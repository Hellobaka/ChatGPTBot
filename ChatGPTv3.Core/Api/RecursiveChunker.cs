using System.Text;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// 递归字符切分器（Recursive Character Chunker）。
/// 按分隔符优先级从粗到细逐级拆分，尽量在自然语义边界（段落、句子）处断开。
/// 零成本、零外部依赖，适用于 RAG 知识库导入场景。
/// </summary>
public static class RecursiveChunker
{
    /// <summary>
    /// 分隔符优先级列表：句子结束标点优先，确保句子完整性。
    /// 每一级都会尝试将片段拆分；若拆分后各段仍超过目标大小，则进入下一级。
    /// </summary>
    private static readonly string[] Separators =
    [
        "。",        // 中文句号（最高优先级：保留句子完整）
        "！",        // 中文感叹号
        "？",        // 中文问号
        "；",        // 中文分号
        ".",         // 英文句号
        "!",         // 英文感叹号
        "?",         // 英文问号
        "\n\n\n",    // 多段落
        "\n\n",      // 段落
        "\n",        // 换行
        ",",         // 英文逗号
        "，",        // 中文逗号
        " ",         // 空格
        "",          // 字符级硬切（兜底）
    ];

    /// <summary>
    /// 将文本递归切分为若干段。
    /// </summary>
    /// <param name="text">待切分的原始文本</param>
    /// <param name="chunkSize">目标每段字符数（实际可能略大，以保证语义完整）</param>
    /// <param name="overlap">相邻段之间的重叠字符数</param>
    /// <returns>切分后的文本段数组</returns>
    public static string[] Chunk(string text, int chunkSize = 300, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        chunkSize = Math.Max(1, chunkSize);
        overlap = Math.Max(0, overlap);

        // 第一级：整段文本作为初始片段
        var chunks = new List<string> { text };

        foreach (var sep in Separators)
        {
            var nextChunks = new List<string>();
            bool needsFurtherSplit = false;

            foreach (var chunk in chunks)
            {
                if (chunk.Length <= chunkSize)
                {
                    nextChunks.Add(chunk);
                    continue;
                }

                needsFurtherSplit = true;

                if (string.IsNullOrEmpty(sep))
                {
                    // 字符级硬切兜底
                    for (int i = 0; i < chunk.Length; i += chunkSize)
                    {
                        nextChunks.Add(chunk.Substring(i, Math.Min(chunkSize, chunk.Length - i)));
                    }
                }
                else
                {
                    // 按当前分隔符拆分，保留分隔符（附在前一段末尾）
                    var parts = SplitWithSeparator(chunk, sep);
                    var merged = MergeSmallParts(parts, chunkSize);
                    nextChunks.AddRange(merged);
                }
            }

            chunks = nextChunks;

            // 如果本轮没有发生任何拆分，说明已经足够细，提前退出
            if (!needsFurtherSplit)
            {
                break;
            }
        }

        // 过滤空段
        var result = chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        // 添加重叠
        if (overlap > 0 && result.Count > 1)
        {
            result = AddOverlap(result, overlap);
        }

        // 去除每段前后空白
        return result.Select(c => c.Trim()).ToArray();
    }

    /// <summary>
    /// 将过小的相邻片段合并，避免产生大量碎片。
    /// 合并条件：当前片段 + 下一片段 <= chunkSize。
    /// </summary>
    private static List<string> MergeSmallParts(string[] parts, int chunkSize)
    {
        var merged = new List<string>();
        var current = new StringBuilder();

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (current.Length == 0)
            {
                current.Append(part);
            }
            else if (current.Length + part.Length <= chunkSize)
            {
                current.Append(part);
            }
            else
            {
                merged.Add(current.ToString());
                current.Clear();
                current.Append(part);
            }
        }

        if (current.Length > 0)
        {
            merged.Add(current.ToString());
        }

        return merged;
    }

    /// <summary>
    /// 将文本按分隔符拆分，并将分隔符保留在前一段的末尾。
    /// 例如："A。B。C" 按 "。" 拆分 → ["A。", "B。", "C"]
    /// </summary>
    private static string[] SplitWithSeparator(string text, string sep)
    {
        if (string.IsNullOrEmpty(sep))
        {
            // 字符级硬切：每个字符作为一段
            return text.Select(c => c.ToString()).ToArray();
        }

        var parts = text.Split([sep], StringSplitOptions.RemoveEmptyEntries);
        // 将分隔符附加到除最后一段外的每一段末尾
        for (int i = 0; i < parts.Length - 1; i++)
        {
            parts[i] += sep;
        }
        return parts;
    }

    /// <summary>
    /// 为相邻段添加重叠内容。
    /// 第 i+1 段的前 overlap 个字符 = 第 i 段的末尾 overlap 个字符。
    /// </summary>
    private static List<string> AddOverlap(List<string> chunks, int overlap)
    {
        var result = new List<string>(chunks.Count);
        result.Add(chunks[0]);

        for (int i = 1; i < chunks.Count; i++)
        {
            var prev = chunks[i - 1];
            var current = chunks[i];

            var overlapText = prev.Length <= overlap
                ? prev
                : prev.Substring(prev.Length - overlap);

            result.Add(overlapText + current);
        }

        return result;
    }
}

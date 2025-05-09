# ChatGPT插件
- 寻找前缀触发的旧模式请前往[v1分支](https://github.com/Hellobaka/ChatGPTBot/tree/v1)
- 触发、Prompt以及关系心情等模块逻辑抄自[MaiBot](https://github.com/MaiM-with-u/MaiBot)

## 功能
- 记忆模块，由[Qdrant](https://github.com/qdrant/qdrant)提供向量数据库。
- 心情模块
- 关系模块
- 自适应回复意愿
- 表情包感知与刮削，需要上游框架支持传递`sub_type`参数
- 消息分段
- Token 开销统计

## 自v1更新
建议删除配置文件，并重新配置

## 自搭Qdrant
Qdrant提供 Docker 版本  
不喜欢 Docker 这有配好的 [1.13.6-windows](http://assets.hellobaka.xyz/static/qdrant-1.13.6.zip)  

### 创建集合时崩溃？
安装最新的[vc++运行库](https://aka.ms/vs/17/release/vc_redist.x64.exe)

> [!WARNING]
> 若更改Embedding的模型，请执行以下操作
> 1. 重新配置Qdrant
> 2. 从`core.db`中，删除`Picture`表

## 依赖文件
- [libHarfBuzzSharp.dll](https://github.com/Hellobaka/BilibiliUpdateCheckBot/releases/download/2.0.0/libHarfBuzzSharp.dll)
- [libSkiaSharp.dll](https://github.com/Hellobaka/BilibiliUpdateCheckBot/releases/download/2.0.0/libSkiaSharp.dll)
- [SQLite.Interop.dll](https://github.com/Hellobaka/WordCloud/releases/download/1.0.0/SQLite.Interop.dll)
- 放置在框架/加载器的
- 1. 根目录 或者
  2. x86 文件夹 或者
  3. libraies 文件夹

## 配置文件
配置文件支持热重载，保存后若格式无误则会即时生效

| Key                              | Value                                      | Description                              |
|----------------------------------|--------------------------------------------|------------------------------------------|
| EnableGroupReply                 | false                                      | 群聊是否使用回复                    |
| StreamMode                       | true                                       | 是否启用流式模式                        |
| EnableTTS                        | false                                      | 是否启用TTS                      |
| TTSVoice                         | zh-CN-YunxiNeural                         | TTS使用的语音模型                           |
| ChatAPIKey                       | ""                                         | ChatGPT API的密钥                           |
| BochaAPIKey                      | ""                                         | Bocha API的密钥                         |
| ChatBaseURL                      | https://api.openai.com/v1                  | ChatGPT API的URL                        |
| ChatModelName                    | gpt-4o                                     | 聊天使用的模型名称                      |
| MasterQQ                         | 114514                                     | 管理员QQ号码                                 |
| ChatMaxTokens                    | 3000                                       | 聊天最大Token数量                       |
| ChatTemperature                   | 1.3                                        | 聊天的温度参数                          |
| ImageGenerateSize                | 2                                          | 图片生成尺寸                            |
| ImageGenerateQuality             | 1                                          | 图片生成质量                            |
| GroupList                        | []                                         | 启用的群组列表                                 |
| PersonList                       | []                                         | 启用的私聊列表                                 |
| BlackList                        | []                                         | 黑名单                                  |
| ImageGenerationOrder             | .画图                                      | 图片生成命令                             |
| AddBlackListOrder                | .添加黑名单                                 | 添加黑名单命令                           |
| RemoveBlackListOrder             | .移除黑名单                                 | 移除黑名单命令                           |
| BotName                          | ChatGPT                                    | 机器人名称（Prompt使用）                              |
| BotNicknames                     | ["ChatGPT"]                                | 机器人昵称列表                           |
| GroupPrompt                      | 胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。,现在请你读读之前的聊天记录，然后给出日常且口语化的回复，平淡一些，尽量简短一些。感觉有趣也可以直接复读消息。请注意把握聊天内容，不要刻意突出自身学科背景，不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。 | 群组的Prompt                           |
| PrivatePrompt                    | 胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。现在请你读读之前的聊天记录，然后给出日常且口语化的回复，尽量简短一些。请注意把握聊天内容，不要刻意突出自身学科背景，不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。 | 私聊的Prompt                           |
| EnableVision                     | true                                       | 是否启用视觉                        |
| EnableSpliter                    | false                                      | 是否启用分段功能                        |
| SpliterModelName                 | gpt-4o-mini                                | 使用的分段模型名称                      |
| SpliterPrompt                    | 请将后续输入的一段话，按符合正常人节奏与习惯，最大分段不能超过$MaxLines$段。分段拆分成Json数组，示例格式：['语句1', '语句2']。注意一定不要有影响到json格式的其他内容输出。上下文相关性很强的内容，一定要单独占一段，不得分开。不得精简我提供的内容，一定不得更改我的输入文本。每个分段结尾只能有问号、叹号或者省略号，逗号句号都不要。 | 分段提示文本                            |
| SpliterMaxLines                  | 3                                          | 分段的最大行数                          |
| SpliterRegexFirst                | false                                      | 是否只使用正则表达式处理                   |
| SpliterRegexRemovePunctuation    | false                                      | 正则模式是否移除末尾标点符号                        |
| SpliterSimulateTypeSpeed         | 100                                        | 模拟的打字速度（字/分钟）                          |
| EnableSpliterRandomDelay         | true                                       | 是否启用随机延迟                        |
| EnableEmojiSend                  | false                                      | 是否启用表情发送                        |
| SpliterRandomDelayMin            | 1000                                       | 随机延迟的最小值                        |
| SpliterRandomDelayMax            | 4500                                       | 随机延迟的最大值                        |
| SpliterMinLength                 | 10                                         | 使用分段的最小文本长度                          |
| EmojiSendProbablity              | 10                                         | 发送表情的概率                          |
| ContextMaxLength                 | 20                                         | 携带的上下文的最大数量                        |
| SpliterUrl                       | https://api.openai.com/v1                  | 分段功能的API URL                       |
| SpliterApiKey                    | ""                                         | 分段功能的API密钥                       |
| ImageDescriberUrl                | https://api.openai.com/v1                  | 图像描述功能的API URL                   |
| ImageDescriberApiKey             | ""                                         | 图像描述功能的API密钥                   |
| ImageDescriberModelName          | gpt-4o-mini                                | 图像描述使用的模型名称                  |
| EmbeddingUrl                     | https://api.openai.com/v1/embeddings       | Embedding 功能的API URL                       |
| EmbeddingApiKey                  | ""                                         | Embedding 功能的API密钥                       |
| EmbeddingModelName               | text-embedding-ada-002                     | Embedding 功能使用的模型名称                  |
| EnableRerank                     | true                                       | 是否启用 Rerank 功能                    |
| RerankUrl                        | https://lkeap.tencentcloudapi.com          | Rerank 功能的API URL                   |
| RerankApiKey                     | ""                                         | Rerank 功能的API密钥                   |
| RerankModelName                  | lke-reranker-base                          | Rerank 使用的模型名称                  |
| IgnoreNotEmoji                   | true                                       | 是否忽略非表情内容                      |
| DebugMode                        | false                                      | 是否启用调试模式                        |
| SchedulePrompt                   | 喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜 | 获取日程的人格文本                           |
| DefaultSchedule                  | 摸鱼                                       | 默认日程安排                            |
| ChatEmptyResponse                | <EMPTY>                                    | 空聊天回应                              |
| RandomSendEmoji                  | true                                       | 是否随机发送表情                        |
| RecommendEmojiCount              | 5                                          | 推荐的表情数量                          |
| EnableMemory                     | true                                       | 是否启用记忆功能                        |
| MinMemorySimilarty               | 0.8                                        | 最小记忆相似度                          |
| MaxMemoryCount                   | 5                                          | 最大记忆数量                            |
| EnableTencentSign                | false                                      | 是否启用腾讯云签名(v3)功能                    |
| TencentSecretKey                 | ""                                         | 腾讯云密钥                                 |
| TencentSecretId                  | ""                                         | 腾讯云秘钥ID                               |
| QdrantHost                       | localhost                                  | Qdrant 服务主机                           |
| QdrantPort                       | 6333                                       | Qdrant 服务端口                           |
| QdrantAPIKey                     | aFZsX4Xe2pzWybnX61Vi                      | Qdrant 的API密钥                         |
| QdrantSearchOnlyPerson           | false                                      | Qdrant 是否只搜索个人记忆                          |
| ReplyWillingAmplifier           | 1                                      | 回复意愿倍率                          |
|ImageGenerationTimeout |30000|图片生成接口超时(ms)|
|RelationshipUpdateTime   |7|名片缓存过期时间(天)|
|EmbeddingTimeout   |3000|Embedding接口超时(ms)|
|RerankTimeout   |3000|Rerank接口超时(ms)|
|ChatTimeout   |30000|聊天接口超时(ms)|
|MemoryDimensions   |1024|向量数据库维度数|

## TODO
- [ ] 使用 ToolCalling 扩展Bot能力：可能有联网搜索、主动记忆召回、主动记忆添加
- [x] UI: 配置窗口
- [x] UI: Token 用量统计 (分接口、月度、分小时、图表)
- [x] UI: 回复意愿、心情等，实时监看与修改
- [ ] ~实现对Vist变声器的TTS对接~
- [ ] 睡觉时间段特殊回复
- [ ] 知识库
- [ ] 其他的饼

using System;
using System.Text.Json;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class BochaSearch
    {
        public const string ErrorMessage = "联网搜索发生异常，{0}";
        public const string BaseUrl = "https://api.bochaai.com/v1/web-search";

        public static string ToolParameters { get; set; } = """
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "搜索关键字或语句",
                        "example": "阿里巴巴2024年的ESG报告"
                        },
                    "freshness": {
                        "type": "string",
                        "description": "搜索指定时间范围内的网页（可选值 \"noLimit\"、\"oneDay\"、\"oneWeek\"、\"oneMonth\"、\"oneYear\"）",
                        "default": "noLimit",
                        "enum": [
                            "noLimit",
                            "oneDay",
                            "oneWeek",
                            "oneMonth",
                            "oneYear"
                        ],
                        "example": "noLimit"
                    },
                    "summary": {
                        "type": "boolean",
                        "description": "是否显示文本摘要",
                        "default": false
                    },            
                    "count": {
                        "type": "integer",
                        "description": "返回的搜索结果数量（1-10），默认为10",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 10,
                        "example": 10
                    },
                    "page": {
                        "type": "integer",
                        "description": "搜索结果页码",
                        "default": 1
                        }
                },
                "required": ["query"]
            }
            """;

        public static string ToolSchema { get; set; } = """
            { 
                "responses": {
                    "200": {
                    "description": "成功的搜索响应",
                    "content": {
                    "application/json": {
                        "schema": {
                        "type": "object",
                        "properties": {
                            "code": {
                            "type": "integer",
                            "description": "响应的状态码",
                            "example": 200
                            },
                            "log_id": {
                            "type": "string",
                            "description": "请求的唯一日志ID",
                            "example": "0d0eb34abc6eec9d"
                            },
                            "msg": {
                            "type": "string",
                            "nullable": true,
                            "description": "请求的消息提示（如果有的话）",
                            "example": null
                            },
                            "data": {
                            "type": "object",
                            "properties": {
                                "_type": {
                                "type": "string",
                                "description": "搜索的类型",
                                "example": "SearchResponse"
                                },
                                "queryContext": {
                                "type": "object",
                                "properties": {
                                    "originalQuery": {
                                    "type": "string",
                                    "description": "原始的搜索关键字",
                                    "example": "阿里巴巴2024年的ESG报告"
                                    }
                                }
                                },
                                "webPages": {
                                "type": "object",
                                "properties": {
                                    "webSearchUrl": {
                                    "type": "string",
                                    "description": "网页搜索的URL",
                                    "example": "https://bochaai.com/search?q=阿里巴巴2024年的ESG报告"
                                    },
                                    "totalEstimatedMatches": {
                                    "type": "integer",
                                    "description": "搜索匹配的网页总数",
                                    "example": 1618000
                                    },
                                    "value": {
                                    "type": "array",
                                    "items": {
                                        "type": "object",
                                        "properties": {
                                        "id": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "网页的排序ID"
                                        },
                                        "name": {
                                            "type": "string",
                                            "description": "网页的标题"
                                        },
                                        "url": {
                                            "type": "string",
                                            "description": "网页的URL"
                                        },
                                        "displayUrl": {
                                            "type": "string",
                                            "description": "网页的展示URL"
                                        },
                                        "snippet": {
                                            "type": "string",
                                            "description": "网页内容的简短描述"
                                        },
                                        "summary": {
                                            "type": "string",
                                            "description": "网页内容的文本摘要"
                                        },
                                        "siteName": {
                                            "type": "string",
                                            "description": "网页的网站名称"
                                        },
                                        "siteIcon": {
                                            "type": "string",
                                            "description": "网页的网站图标"
                                        },
                                        "dateLastCrawled": {
                                            "type": "string",
                                            "format": "date-time",
                                            "description": "网页的收录时间或发布时间"
                                        },
                                        "cachedPageUrl": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "网页的缓存页面URL"
                                        },
                                        "language": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "网页的语言"
                                        },
                                        "isFamilyFriendly": {
                                            "type": "boolean",
                                            "nullable": true,
                                            "description": "是否为家庭友好的页面"
                                        },
                                        "isNavigational": {
                                            "type": "boolean",
                                            "nullable": true,
                                            "description": "是否为导航性页面"
                                        }
                                        }
                                    }
                                    }
                                }
                                },
                                "images": {
                                "type": "object",
                                "properties": {
                                    "id": {
                                    "type": "string",
                                    "nullable": true,
                                    "description": "图片搜索结果的ID"
                                    },
                                    "webSearchUrl": {
                                    "type": "string",
                                    "nullable": true,
                                    "description": "图片搜索的URL"
                                    },
                                    "value": {
                                    "type": "array",
                                    "items": {
                                        "type": "object",
                                        "properties": {
                                        "webSearchUrl": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片搜索结果的URL"
                                        },
                                        "name": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片的名称"
                                        },
                                        "thumbnailUrl": {
                                            "type": "string",
                                            "description": "图像缩略图的URL",
                                            "example": "http://dayu-img.uc.cn/columbus/img/oc/1002/45628755e2db09ccf7e6ea3bf22ad2b0.jpg"
                                        },
                                        "datePublished": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图像的发布日期"
                                        },
                                        "contentUrl": {
                                            "type": "string",
                                            "description": "访问全尺寸图像的URL",
                                            "example": "http://dayu-img.uc.cn/columbus/img/oc/1002/45628755e2db09ccf7e6ea3bf22ad2b0.jpg"
                                        },
                                        "hostPageUrl": {
                                            "type": "string",
                                            "description": "图片所在网页的URL",
                                            "example": "http://dayu-img.uc.cn/columbus/img/oc/1002/45628755e2db09ccf7e6ea3bf22ad2b0.jpg"
                                        },
                                        "contentSize": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片内容的大小"
                                        },
                                        "encodingFormat": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片的编码格式"
                                        },
                                        "hostPageDisplayUrl": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片所在网页的显示URL"
                                        },
                                        "width": {
                                            "type": "integer",
                                            "description": "图片的宽度",
                                            "example": 553
                                        },
                                        "height": {
                                            "type": "integer",
                                            "description": "图片的高度",
                                            "example": 311
                                        },
                                        "thumbnail": {
                                            "type": "string",
                                            "nullable": true,
                                            "description": "图片缩略图（如果有的话）"
                                        }
                                        }
                                    }
                                    }
                                }
                                },
                                "videos": {
                                "type": "object",
                                "properties": {
                                    "id": {
                                    "type": "string",
                                    "nullable": true,
                                    "description": "视频搜索结果的ID"
                                    },
                                    "readLink": {
                                    "type": "string",
                                    "nullable": true,
                                    "description": "视频的读取链接"
                                    },
                                    "webSearchUrl": {
                                    "type": "string",
                                    "nullable": true,
                                    "description": "视频搜索的URL"
                                    },
                                    "isFamilyFriendly": {
                                    "type": "boolean",
                                    "description": "是否为家庭友好的视频"
                                    },
                                    "scenario": {
                                    "type": "string",
                                    "description": "视频的场景"
                                    },
                                    "value": {
                                    "type": "array",
                                    "items": {
                                        "type": "object",
                                        "properties": {
                                        "webSearchUrl": {
                                            "type": "string",
                                            "description": "视频搜索结果的URL"
                                        },
                                        "name": {
                                            "type": "string",
                                            "description": "视频的名称"
                                        },
                                        "description": {
                                            "type": "string",
                                            "description": "视频的描述"
                                        },
                                        "thumbnailUrl": {
                                            "type": "string",
                                            "description": "视频的缩略图URL"
                                        },
                                        "publisher": {
                                            "type": "array",
                                            "items": {
                                            "type": "object",
                                            "properties": {
                                                "name": {
                                                "type": "string",
                                                "description": "发布者名称"
                                                }
                                            }
                                            }
                                        },
                                        "creator": {
                                            "type": "object",
                                            "properties": {
                                            "name": {
                                                "type": "string",
                                                "description": "创作者名称"
                                            }
                                            }
                                        },
                                        "contentUrl": {
                                            "type": "string",
                                            "description": "视频内容的URL"
                                        },
                                        "hostPageUrl": {
                                            "type": "string",
                                            "description": "视频所在网页的URL"
                                        },
                                        "encodingFormat": {
                                            "type": "string",
                                            "description": "视频编码格式"
                                        },
                                        "hostPageDisplayUrl": {
                                            "type": "string",
                                            "description": "视频所在网页的显示URL"
                                        },
                                        "width": {
                                            "type": "integer",
                                            "description": "视频的宽度"
                                        },
                                        "height": {
                                            "type": "integer",
                                            "description": "视频的高度"
                                        },
                                        "duration": {
                                            "type": "string",
                                            "description": "视频的长度"
                                        },
                                        "motionThumbnailUrl": {
                                            "type": "string",
                                            "description": "动态缩略图的URL"
                                        },
                                        "embedHtml": {
                                            "type": "string",
                                            "description": "用于嵌入视频的HTML代码"
                                        },
                                        "allowHttpsEmbed": {
                                            "type": "boolean",
                                            "description": "是否允许HTTPS嵌入"
                                        },
                                        "viewCount": {
                                            "type": "integer",
                                            "description": "视频的观看次数"
                                        },
                                        "thumbnail": {
                                                "type": "object",
                                                "properties": {
                                                "height": {
                                                    "type": "integer",
                                                    "description": "视频缩略图的高度"
                                                },
                                                "width": {
                                                    "type": "integer",
                                                    "description": "视频缩略图的宽度"
                                                }
                                            }
                                        },
                                        "allowMobileEmbed": {
                                            "type": "boolean",
                                            "description": "是否允许移动端嵌入"
                                        },
                                        "isSuperfresh": {
                                            "type": "boolean",
                                            "description": "是否为最新视频"
                                        },
                                        "datePublished": {
                                            "type": "string",
                                            "description": "视频的发布日期"
                                        }
                                        }
                                    }
                                    }
                                }
                                }
                            }
                            }
                        }
                        }
                    }
                    }
                },
                "400": {
                    "description": "请求参数错误"
                },
                "401": {
                    "description": "未授权 - API 密钥无效或缺失"
                },
                "500": {
                    "description": "搜索服务内部错误"
                }
            }
            }
            """;

        public static string Search(string query, string freshness = "noLimit", bool summary = false, int count = 10, int page = 1)
        {
            if (string.IsNullOrEmpty(query))
            {
                return string.Format(ErrorMessage, "参数 query 为空");
            }
            if (string.IsNullOrEmpty(AppConfig.BochaAPIKey))
            {
                return string.Format(ErrorMessage, "搜索 APIKey 为空");
            }
            MainSave.CQLog?.Debug("搜索", $"query={query} freshness={freshness} summary={summary} count={count} page={page}");
            return CommonHelper.Post("POST", BaseUrl, new
            {
                query,
                freshness,
                summary,
                count,
                page
            }.ToJson(), AppConfig.BochaAPIKey) ?? string.Format(ErrorMessage, "发送搜索请求失败");
        }

        public static string HandleToolCall(BinaryData functionArguments)
        {
            using JsonDocument argumentsJson = JsonDocument.Parse(functionArguments);
            bool hasQuery = argumentsJson.RootElement.TryGetProperty("query", out JsonElement jsonQuery);
            bool hasFreshness = argumentsJson.RootElement.TryGetProperty("freshness", out JsonElement jsonFreshness);
            bool hasSummary = argumentsJson.RootElement.TryGetProperty("summary", out JsonElement jsonSummary);
            bool hasCount = argumentsJson.RootElement.TryGetProperty("count", out JsonElement jsonCount);
            bool hasPage = argumentsJson.RootElement.TryGetProperty("page", out JsonElement jsonPage);

            string query = jsonQuery.ToString();
            string freshness = !hasFreshness ? "noLimit" : jsonFreshness.ToString();
            bool summary = hasSummary && jsonSummary.GetBoolean();
            int count = hasCount ? jsonCount.GetInt32() : 5;
            int page = hasPage ? jsonPage.GetInt32() : 1;

            return Search(query, freshness, summary, count, page);
        }
    }
}

# ChatGPT插件

## 功能
- 功能支持群组与私聊
- 上下文根据QQ绑定，与群ID无关
- 若`EnableVision`开启时，发送图片时会自动将模型切换为`gpt-4-vision-preview`
- 非`Another-Mirai-Native`框架时打开`EnableGroupReply`将可能导致崩溃

## 配置文件
配置文件支持热重载，保存后若格式无误则会即时生效

| Key                        | Value | Description                              |
|----------------------------|-------|------------------------------------------|
| AddPromptOrder             | 添加预设 | 添加预设的指令 |
| APIKey                     |       | 调用接口需要的Key |
| AppendExecuteTime          | true | 消息末尾添加执行时间 |
| AtResponse                 | false | 收到@消息时触发回复 |
| BaseURL                    | https://api.openai.com | 接口基础Url，例如：https://api.openai.com/v1/chat/completions 需要填入此处的为https://api.openai.com |
| ChatMaxTokens              | 500 | 每次对话消费的最大Token数 |
| ChatPromptOrder            | .预设 | 使用预设的指令，触发后，使用指令的用户在接下来一次对话将会使用预设 |
| ChatTimeout                | 600 | 每次调用最大的超时时长 单位s |
| ContinueModeOrder          | 连续模式 | 连续模式的指令，使用指令的用户无需触发指令与At就可以触发聊天功能 |
| DisableChatOrder           | 关闭聊天 | 禁用本群功能的指令，仅限`MasterQQ`用户调用 |
| EnableChatOrder            | 开启聊天 | 启用本群功能的指令，仅限`MasterQQ`用户调用 |
| EnableGroupReply           | false | 仅限`Another-Mirai-Native`兼容框架，启用之后群消息将会使用回复 |
| EnableVision               | false | 是否允许调用`gpt-4-vision-preview`模型 |
| GroupList                  | [] | 启用的群列表 |
| ImageGenerateQuality       | 0 | 图片生成质量：`1 => HD` `0 => Standard` |
| ImageGenerateSize          | 2 | 图片生成尺寸：`0 => 256x256` `1 => 512x512` `2 => 1024x1024` `3 => 1024x1792` `4 => 1792x1024` 注意：`dall-e-2`模型只可使用0、1、2的尺寸；`dall-e-3`模型只可使用2、3、4的尺寸； |
| ImageGenerationModelName   | dall-e-3 | 图片生成的模型 |
| ImageGenerationOrder       | .画图 | 调用图片生成的指令 |
| ListPromptOrder            | 预设列表 | 列举所有预设的指令 |
| MasterQQ                   | 114514 | 允许使用指令开启、关闭群组功能的QQ |
| ModelName                  | gpt-3.5-turbo-16k | 文本聊天使用的模型 |
| PersonList                 | [] | 允许使用私聊触发聊天的QQ列表，需要手动添加 |
| RemovePromptOrder          | 移除预设 | 移除预设的指令 |
| ResetChatOrder             | 重置聊天 | 重置个人聊天上下文指令 |
| ResponsePrefix             | .chat | 调用聊天的前缀之类 |
| StreamMode                 | true | 流式调用结果，启用后可提降低调用失败的概率 |
| WelcomeText                | 请耐心等待回复... | 触发聊天时发送的文本 |

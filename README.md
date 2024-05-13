# ChatGPT插件

## 功能
- 功能支持群组与私聊
- 上下文根据调用来源不同绑定不同ID：群组绑定群ID；私聊绑定QQ
- 若`EnableVision`开启时，发送图片时会自动将模型切换为`gpt-4-vision-preview`
- 非`Another-Mirai-Native`框架时打开`EnableGroupReply`将可能导致崩溃

## TTS功能
- 使用[edge-tts](https://github.com/rany2/edge-tts)库
- 请按照第三方库中提及的方式进行安装，保证能够使用`cmd`访问`python`以及`edge-tts`

## 配置文件
配置文件支持热重载，保存后若格式无误则会即时生效

| Key                        | Value | Description                              |
|----------------------------|-------|------------------------------------------|
| AddPromptOrder             | 添加预设 | 添加预设的指令 |
| APIKey                     |       | 调用接口需要的Key |
| AppendExecuteTime          | true | 消息末尾添加执行时间 |
| EnableTTS                  | false | 启用TTS功能 |
| SendTextBeforeTTS          | true | 是否在发送语音前发送文本，关闭后只有语音发送 |
| SendErrorTextWhenTTSFail   | false | 在语音合成失败之后，额外发送一个失败消息 |
| TTSVoice                   | zh-CN-YunxiNeural | 调用TTS的语言模型，可用`edge-tts --list-voices`查看可用列表 |
| AtResponse                 | false | 收到@消息时触发回复 |
| BaseURL                    | https://api.openai.com | 接口基础Url，例如：https://api.openai.com/v1/chat/completions 需要填入此处的为https://api.openai.com |
| ChatMaxTokens              | 500 | 每次对话消费的最大Token数 |
| ChatPromptOrder            | .预设 | 使用预设的指令，触发后，使用指令的用户在接下来一次对话将会使用预设 |
| ChatTimeout                | 600 | 每次调用最大的超时时长 单位s |
| ContinueModeOrder          | 连续模式 | 连续模式的指令，使用指令的用户无需触发指令与At就可以触发聊天功能 |
| DisableChatOrder           | 关闭聊天 | 禁用本群功能的指令，仅限`MasterQQ`用户调用 |
| EnableChatOrder            | 开启聊天 | 启用本群功能的指令，仅限`MasterQQ`用户调用 |
| AddBlackListOrder          | .添加黑名单 | 私聊添加黑名单的指令，仅限`MasterQQ`用户调用 |
| RemoveBlackListOrder       | .移除黑名单 | 私聊移除黑名单的指令，仅限`MasterQQ`用户调用 |
| EnableGroupReply           | false | 仅限`Another-Mirai-Native`兼容框架，启用之后群消息将会使用回复 |
| EnableVision               | false | 是否允许调用`gpt-4-vision-preview`模型 |
| GroupList                  | [] | 启用的群列表 |
| BlackList                  | [] | 全局黑名单 |
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
| AppendGroupNick            | false | 是否在群组对话时附加对话者的昵称与QQ |
| BotName                    | ChatGPT | 主动提供的机器人昵称 |
| PrivatePrompt              | Current model: `$ModelName$`. Current time: `$Time$`.你的昵称是: `$BotName$` | 私聊时使用的Prompt |
| GroupPrompt                | Current model: `$ModelName$`. Current time: `$Time$`.你的昵称是: `$BotName$`你当前在一个QQ群中，你需要区分不同人发送的消息并给出符合群组气氛的回答。QQ号即是ID。根据配置不同，客户端传递的信息格式也不同。若每条信息满足 昵称[QQ]: 消息 的格式时，客户端会向你提供发言者昵称以及ID，可以依次区分不同的发言者。当用户昵称为“未获取到昵称”时代表客户端真的无法获取此用户的昵称，请使用ID区分。你在发言时无需附加这个格式，只需要回复信息即可。另外，如果你需要At对话者，请使用<@QQ>的格式，例如<@123456>，同理，如果用户提供这个格式表示他需要指向这个人，请从上下文了解这个人的发言历史以及个人信息 | 群组使用的Prompt |

## 文本模板
| Key                        | Description                              |
|----------------------------|------------------------------------------|
|`$ModelName$`|当前使用的模型名称|
|`$Time$`|当前时间|
|`$BotName$`|机器人昵称|
|`$Id$`|调用者ID|
|`$GroupName$`|群组名称|
|`<@QQ>`|转换为At消息的模板|

using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.ViewModel
{
    public class MCPToolTestViewModel : INotifyPropertyChanged
    {
        public MCPToolTestViewModel(MCPToolModel toolModel)
        {
            if (toolModel == null)
            {
                return;
            }
            MCPToolModel = toolModel;
            CreateArguments();
        }

        public string ToolName { get; set; }

        public string ToolDescription { get; set; }

        public ObservableCollection<ToolArgumentItem> Arguments { get; set; } = [];

        public JsonObject CreatedRequest { get; set; }

        public string ParsedRequest { get; set; }

        public string Response { get; set; }

        public bool Requesting { get; set; }

        private MCPToolModel MCPToolModel { get; set; }

        private static JsonSerializerOptions JsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task SendRequest()
        {
            if (MCPToolModel == null || MCPToolModel.Tool == null)
            {
                return;
            }
            Requesting = true;
            try
            {
                Dictionary<string, object> argument = [];
                foreach (var item in Arguments)
                {
                    var value = TryParseArgument(item, false);
                    if (value == null)
                    {
                        if (!string.IsNullOrEmpty(item.DefaultValue))
                        {
                            value = TryParseArgument(item, true);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    argument.Add(item.ArgumentName, value);
                }
                var response = await MCPToolModel.Tool.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(argument));
                if (response is JsonElement json)
                {
                    Response = JsonSerializer.Serialize(json, JsonSerializerOptions);
                }
                else
                {
                    Response = response?.ToString() ?? "无返回值";
                }
            }
            catch (Exception e)
            {
                MainWindow.ShowError($"发送 Tool 测试请求时发生错误：{e}");
            }
            finally
            {
                Requesting = false;
            }
        }

        private object? TryParseArgument(ToolArgumentItem item, bool isDefault)
        {
            return item.ArgumentType.ToLower() switch
            {
                "string" => isDefault ? item.DefaultValue : item.Value,
                "integer" => int.TryParse(isDefault ? item.DefaultValue : item.Value, out var num) ? num : (int?)null,
                "float" or "double" => double.TryParse(isDefault ? item.DefaultValue : item.Value, out var num) ? num : (double?)null,
                "boolean" => bool.TryParse(isDefault ? item.DefaultValue : item.Value, out var b) ? b : (bool?)null,
                _ => null,
            };
        }

        private void CreateArguments()
        {
            Arguments = [];
            if (MCPToolModel.Tool == null)
            {
                return;
            }
            ToolName = MCPToolModel.Tool.Name;
            ToolDescription = MCPToolModel.Tool.Description;
            foreach (var item in MCPToolModel.Tool.JsonSchema.EnumerateObject())
            {
                if (item.Name == "properties")
                {
                    foreach (var arg in item.Value.EnumerateObject())
                    {
                        var argumentItem = new ToolArgumentItem
                        {
                            ArgumentName = arg.Name,
                            ArgumentType = arg.Value.TryGetProperty("type", out var defaultValue) ? defaultValue.ToString() : "无指定类型",
                            DefaultValue = arg.Value.TryGetProperty("default", out defaultValue) ? defaultValue.ToString() : string.Empty,
                            Description = arg.Value.TryGetProperty("description", out defaultValue) ? defaultValue.ToString() : "无描述",
                        };
                        argumentItem.PropertyChanged += ArgumentItem_PropertyChanged;
                        Arguments.Add(argumentItem);
                    }
                }
                else if (item.Name == "required")
                {
                    foreach (var req in item.Value.EnumerateArray())
                    {
                        var argName = req.GetString();
                        var argumentItem = Arguments.FirstOrDefault(a => a.ArgumentName == argName);
                        if (argumentItem != null)
                        {
                            argumentItem.Required = true;
                        }
                    }
                }
            }
        }

        private void ArgumentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ToolArgumentItem.Value))
            {
                RecreateRequestJson();
            }
        }

        private void RecreateRequestJson()
        {
            CreatedRequest = new JsonObject();
            var param = new JsonObject();
            var arguments = new JsonObject();
            CreatedRequest.Add("method", "tools/call");
            CreatedRequest.Add("params", param);
            param.Add("name", ToolName);
            param.Add("arguments", arguments);
            foreach (var item in Arguments)
            {
                JsonNode valueNode = item.ArgumentType.ToLower() switch
                {
                    "string" => item.Value != null ? JsonValue.Create(item.Value) : null,
                    "integer" => long.TryParse(item.Value, out var num) ? JsonValue.Create(num) : null,
                    "float" or "double" => double.TryParse(item.Value, out var num) ? JsonValue.Create(num) : null,
                    "boolean" => bool.TryParse(item.Value, out var b) ? JsonValue.Create(b) : null,
                    _ => null,
                };
                JsonNode defaultNode = item.ArgumentType.ToLower() switch
                {
                    "string" => item.DefaultValue != null ? JsonValue.Create(item.Value) : null,
                    "integer" => long.TryParse(item.DefaultValue, out var num) ? JsonValue.Create(num) : 0,
                    "float" or "double" => double.TryParse(item.DefaultValue, out var num) ? JsonValue.Create(num) : 0,
                    "boolean" => bool.TryParse(item.DefaultValue, out var b) ? JsonValue.Create(b) : false,
                    _ => null,
                };
                if (string.IsNullOrEmpty(item.Value))
                {
                    if (!string.IsNullOrEmpty(item.DefaultValue))
                    {
                        arguments.Add(item.ArgumentName, defaultNode);
                    }
                }
                else
                {
                    arguments.Add(item.ArgumentName, valueNode);
                }
            }

            ParsedRequest = CreatedRequest.ToJsonString(JsonSerializerOptions);
        }
    }
}

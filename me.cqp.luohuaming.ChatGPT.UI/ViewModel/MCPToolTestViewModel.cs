using me.cqp.luohuaming.ChatGPT.UI.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace me.cqp.luohuaming.ChatGPT.UI.ViewModel
{
    public class MCPToolTestViewModel : INotifyPropertyChanged
    {
        public MCPToolTestViewModel(MCPToolModel toolModel)
        {
            MCPToolModel = toolModel;
            CreateArguments();
        }

        public string ToolName { get; set; }

        public string ToolDescription { get; set; }

        public ObservableCollection<ToolArgumentItem> Arguments { get; set; } = [];

        public JsonObject CreatedRequest { get; set; }

        public string ParsedRequest { get; set; }

        public string Response { get; set; }

        private MCPToolModel MCPToolModel { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CreateArguments()
        {
            Arguments = [];
            if (MCPToolModel.Tool == null)
            {
                return;
            }
            foreach (var item in MCPToolModel.Tool.JsonSchema.EnumerateObject())
            {
                if (item.Name == "title")
                {
                    ToolName = item.Value.GetString();
                }
                else if (item.Name == "description")
                {
                    ToolDescription = item.Value.GetString();
                }
                else if (item.Name == "properties")
                {
                    foreach (var arg in item.Value.EnumerateObject())
                    {
                        var argumentItem = new ToolArgumentItem
                        {
                            ArgumentName = arg.Name,
                            ArgumentType = Enum.TryParse<JsonValueKind>(arg.Value.GetProperty("type").GetString(), false, out var valueKind) ? valueKind : JsonValueKind.Undefined,
                            DefaultValue = arg.Value.TryGetProperty("default", out var defaultValue) ? defaultValue.ToString() : string.Empty,
                            Description = arg.Value.TryGetProperty("description", out defaultValue) ? defaultValue.ToString() : string.Empty,
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
                JsonNode valueNode = item.ArgumentType switch
                {
                    JsonValueKind.String => item.Value != null ? JsonValue.Create(item.Value) : null,
                    JsonValueKind.Number => double.TryParse(item.Value, out var num) ? JsonValue.Create(num) : null,
                    JsonValueKind.True or JsonValueKind.False => bool.TryParse(item.Value, out var b) ? JsonValue.Create(b) : null,
                    _ => null,
                };
                JsonNode defaultNode = item.ArgumentType switch
                {
                    JsonValueKind.String => item.Value != null ? JsonValue.Create(item.DefaultValue) : null,
                    JsonValueKind.Number => double.TryParse(item.DefaultValue, out var num) ? JsonValue.Create(num) : null,
                    JsonValueKind.True or JsonValueKind.False => bool.TryParse(item.DefaultValue, out var b) ? JsonValue.Create(b) : null,
                    _ => null,
                };
                if (string.IsNullOrEmpty(item.Value))
                {
                    if (!item.Required)
                    {
                        continue;
                    }
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

            ParsedRequest = CreatedRequest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

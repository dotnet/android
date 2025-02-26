using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class JsonExtensions
{
    public static JsonNode Merge (this JsonNode jsonBase, JsonNode jsonMerge)
    {
        if (jsonBase == null || jsonMerge == null)
            return jsonBase;

        switch (jsonBase)
        {
            case JsonObject jsonBaseObj when jsonMerge is JsonObject jsonMergeObj: {
                var mergeNodesArray = new KeyValuePair<string, JsonNode?> [jsonMergeObj.Count];
                int index = 0;
                foreach (var prop in jsonMergeObj) {
                    mergeNodesArray [index++] = prop;
                }
                jsonMergeObj.Clear ();

                foreach (var prop in mergeNodesArray) {
                    jsonBaseObj [prop.Key] = jsonBaseObj [prop.Key] switch {
                        JsonObject jsonBaseChildObj when prop.Value is JsonObject jsonMergeChildObj => jsonBaseChildObj.Merge (jsonMergeChildObj),
                        JsonArray jsonBaseChildArray when prop.Value is JsonArray jsonMergeChildArray => jsonBaseChildArray.Merge (jsonMergeChildArray),
                        _ => prop.Value
                    };
                }
                break;
            }
            case JsonArray jsonBaseArray when jsonMerge is JsonArray jsonMergeArray: {
                var mergeNodesArray = new JsonNode? [jsonMergeArray.Count];
                int index = 0;
                foreach (var mergeNode in jsonMergeArray) {
                    mergeNodesArray [index++] = mergeNode;
                }
                jsonMergeArray.Clear ();
                foreach (var mergeNode in mergeNodesArray) {
                    jsonBaseArray.Add (mergeNode);
                }
                break;
            }
            default:
                throw new ArgumentException ($"The JsonNode type [{jsonBase.GetType ().Name}] is incompatible for merging with the target/base " +
                    $"type [{jsonMerge.GetType ().Name}]; merge requires the types to be the same.");
        }
        return jsonBase;
    }

    public static JsonNode? ToNode (this JsonElement element)
    {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                var obj = new JsonObject ();
                foreach (JsonProperty prop in element.EnumerateObject()) {
                    obj [prop.Name] = prop.Value.ToNode ();
                }
                return obj;

            case JsonValueKind.Array:
                var arr = new JsonArray();
                foreach (JsonElement item in element.EnumerateArray ()) {
                    arr.Add (item.ToNode ());
                }
                return arr;

            case JsonValueKind.String:
                return element.GetString ();

            case JsonValueKind.Number:
                return element.TryGetInt32 (out int intValue) ? intValue : element.GetDouble ();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                throw new NotSupportedException ($"Unsupported JSON value kind: {element.ValueKind}");
        }
    }
}
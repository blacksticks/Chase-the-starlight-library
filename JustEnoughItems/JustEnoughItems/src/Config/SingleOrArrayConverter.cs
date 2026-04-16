using System;
using System.Collections.Generic;
using Oculus.Newtonsoft.Json;
using Oculus.Newtonsoft.Json.Linq;

namespace JustEnoughItems.Config
{
    // Allows a JSON property to accept either a single value or an array of values
    public class SingleOrArrayConverter<T> : Oculus.Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<T>);
        }

        public override object ReadJson(Oculus.Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Oculus.Newtonsoft.Json.JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var list = new List<T>();
            if (token.Type == JTokenType.Array)
            {
                foreach (var el in token)
                {
                    using (var r = el.CreateReader())
                    {
                        list.Add(serializer.Deserialize<T>(r));
                    }
                }
            }
            else if (token.Type != JTokenType.Null && token.Type != JTokenType.Undefined)
            {
                using (var r = token.CreateReader())
                {
                    list.Add(serializer.Deserialize<T>(r));
                }
            }
            return list;
        }

        public override void WriteJson(Oculus.Newtonsoft.Json.JsonWriter writer, object value, Oculus.Newtonsoft.Json.JsonSerializer serializer)
        {
            var list = value as List<T>;
            if (list == null || list.Count == 0)
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                return;
            }
            if (list.Count == 1)
            {
                serializer.Serialize(writer, list[0]);
            }
            else
            {
                serializer.Serialize(writer, list);
            }
        }
    }
}

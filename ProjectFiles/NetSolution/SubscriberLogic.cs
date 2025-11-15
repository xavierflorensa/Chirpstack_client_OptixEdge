#region StandardUsing
using System;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.OPCUAServer;
using System.Globalization;
#endregion
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using System.Text.Json.Serialization;
using System.Text.Json;
using FTOptix.WebUI;

public class SubscriberLogic : BaseNetLogic
{
    public override void Start()
    {
        var brokerIpAddressVariable = Project.Current.GetVariable("Model/BrokerIpAddress");

        // Create a client connecting to the broker (default port is 1883)
        subscribeClient = new MqttClient(brokerIpAddressVariable.Value);
        // Connect to the broker
        subscribeClient.Connect("FTOptixSubscribeClient");
        // Assign a callback to be executed when a message is received from the broker
        subscribeClient.MqttMsgPublishReceived += SubscribeClientMqttMsgPublishReceived;
        // Subscribe to the "my_topic" topic with QoS 2
        ushort msgId = subscribeClient.Subscribe(new string[] { "application/9f478050-874e-451d-829d-7906cf5a5386/device/a84041259182c93f/event/up" }, // topic
            //new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE }); // QoS level
            new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE }); // QoS level
        messageVariable = Project.Current.GetVariable("Model/Message");
        distanceStr = Project.Current.GetVariable("Model/Distance_str");
        distanceInt = Project.Current.GetVariable("Model/Distance_int");
        batteryStr = Project.Current.GetVariable("Model/Battery_str"); 
       
    }

    public override void Stop()
    {
        subscribeClient.Unsubscribe(new string[] { "application/9f478050-874e-451d-829d-7906cf5a5386/device/a84041259182c93f/event/up" });
        subscribeClient.Disconnect();
    }

    private void SubscribeClientMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        try
        {
            // Get raw JSON and ensure it's clean
            var rawJson = System.Text.Encoding.Default.GetString(e.Message).Trim();
            
            // Store raw JSON in messageVariable
            messageVariable.Value = rawJson;

            // Parse using JsonDocument for more control
            using (JsonDocument document = JsonDocument.Parse(rawJson))
            {
                var root = document.RootElement;
                
                
                if (root.TryGetProperty("object", out JsonElement objectElement) &&
                    objectElement.TryGetProperty("Distance", out JsonElement distanceElement))
                {
                    string distanceValue = distanceElement.GetString();
                    distanceStr.Value = $"{distanceValue}";
                    
                    // Extract only the numeric part from "683 mm"
                    string numericPart = System.Text.RegularExpressions.Regex.Match(distanceValue, @"\d+").Value;
                    if (int.TryParse(numericPart, out int distanceIntValue))
                    {
                        distanceInt.Value = distanceIntValue;
                    }

                }
                if (root.TryGetProperty("object", out JsonElement objectElementBat) &&
                    objectElementBat.TryGetProperty("Bat", out JsonElement batElement))
                {
                    string batValue = batElement.GetString();
                    batteryStr.Value = $"{batValue}";

                }
            }
        }
        catch (Exception ex)
        {
            distanceStr.Value = $"Error parsing JSON: {ex.Message}";
            Log.Info($"Error parsing JSON: {ex.Message}");
            // Log the raw JSON for debugging
            Log.Info($"Raw JSON: {messageVariable.Value}");
        }
    }
    
    public class ChirpstackUplink
{
    [JsonPropertyName("deduplicationId")]
    public string DeduplicationId { get; set; }

    [JsonPropertyName("time")]
    public string Time { get; set; }

    [JsonPropertyName("deviceInfo")]
    public DeviceInfo DeviceInfo { get; set; }

    [JsonPropertyName("object")]
    public SensorData Object { get; set; }
}

public class DeviceInfo
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; }

    [JsonPropertyName("devEui")]
    public string DevEui { get; set; }
}

public class SensorData
{
    [JsonPropertyName("Bat")]
    public string Bat { get; set; }

    [JsonPropertyName("Distance")]
    public string Distance { get; set; }

    [JsonPropertyName("TempC_DS18B20")]
    public string TempC_DS18B20 { get; set; }

    // Accept either number or quoted number; converter handles both
    [JsonPropertyName("Sensor_flag")]
    public string Sensor_flag { get; set; }

    [JsonPropertyName("Node_type")]
    public string Node_type { get; set; }

    // Accept either number or quoted number; converter handles both
    [JsonPropertyName("Interrupt_flag")]
    public string Interrupt_flag { get; set; }
}

// Converter that reads numbers/booleans/null as strings as well as normal strings
public class FlexibleStringConverter : System.Text.Json.Serialization.JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                // prefer integer representation when possible
                if (reader.TryGetInt64(out long l))
                    return l.ToString(CultureInfo.InvariantCulture);
                // fallback to double for non-integer numbers
                return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean().ToString();
            case JsonTokenType.Null:
                return null;
            default:
                // best-effort fallback
                try
                {
                    return reader.GetString();
                }
                catch
                {
                    return null;
                }
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value);
    }
}

    private MqttClient subscribeClient;
    private IUAVariable messageVariable;
    private IUAVariable distanceStr;
    private IUAVariable batteryStr;
    private IUAVariable distanceInt;

}

/*
// InfluxLP transport commented out for gateway deployment
// This transport is not needed for MQTT-based gateway deployment
// Uncomment if InfluxDB integration is required in the future

using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using l99.driver.@base;
using Scriban;

// ReSharper disable once CheckNamespace
namespace l99.driver.fanuc.transports;

// ReSharper disable once InconsistentNaming
// ReSharper disable once UnusedType.Global
public class InfluxLP : Transport
{
    // runtime - veneer name, template
    private readonly Dictionary<string, Template> _templateLookup = new();

    private InfluxDBClient _client = null!;

    // config - veneer type, template text
    private Dictionary<string, string> _transformLookup = new();
    private WriteApiAsync _writeApi = null!;

    public InfluxLP(Machine machine) : base(machine)
    {
        //TODO: make defaults
    }

    public override async Task<dynamic?> CreateAsync()
    {
        _client = InfluxDBClientFactory
            .Create(
                Machine.Configuration.transport["host"], 
                Machine.Configuration.transport["token"]);

        _writeApi = _client.GetWriteApiAsync();

        _transformLookup = (Machine.Configuration.transport["transformers"] as Dictionary<dynamic, dynamic>)
            .ToDictionary(
                kv => (string) kv.Key,
                kv => (string) kv.Value);

        return null;
    }

    public override async Task ConnectAsync()
    {
    }

    public override async Task SendAsync(params dynamic[] parameters)
    {
        var @event = parameters[0];
        var veneer = parameters[1];
        var data = parameters[2];

        switch (@event)
        {
            case "DATA_ARRIVE":

                if (HasTransform(veneer))
                {
                    string lp = _templateLookup[veneer.Name]
                        .Render(new {data.observation, data.state.data});

                    if (!string.IsNullOrEmpty(lp))
                    {
                        Logger.Info($"[{Machine.Id}] {lp}");
                        _writeApi
                            .WriteRecordAsync(
                                lp,
                                WritePrecision.Ms,
                                Machine.Configuration.transport["bucket"],
                                Machine.Configuration.transport["org"]);
                    }
                }

                break;

            case "SWEEP_END":

                if (HasTransform("SWEEP_END"))
                {
                    var lp = _templateLookup["SWEEP_END"]
                        .Render(new {data.observation, data.state.data});

                    Logger.Info($"[{Machine.Id}] {lp}");

                    _writeApi
                        .WriteRecordAsync(
                            lp,
                            WritePrecision.Ms,
                            Machine.Configuration.transport["bucket"],
                            Machine.Configuration.transport["org"]);
                }

                break;

            case "INT_MODEL":

                break;
        }
    }

    private bool HasTransform(dynamic veneer)
    {
        return _templateLookup.ContainsKey(veneer.Name);
    }

    public override async Task OnGenerateIntermediateModelAsync(dynamic model)
    {
        // TODO: implement
        await Task.FromResult(0);
    }
}
*/
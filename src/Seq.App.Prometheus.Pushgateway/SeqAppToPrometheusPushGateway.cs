﻿using Prometheus.Client;
using Prometheus.Client.MetricPusher;
using Seq.Apps;
using Seq.Apps.LogEvents;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Seq.App.Prometheus.Pushgateway
{
    [SeqApp("Seq.App.Prometheus.Pushgateway",
       Description = "Filtered events are sent to the Prometheus Pushgateway.")]
    class SeqAppToPrometheusPushGateway : SeqApp, ISubscribeTo<LogEventData>
    {
        [SeqAppSetting(
           DisplayName = "Pushgateway URL",
           HelpText = "The URL of the Pushgateway where seq events will be forwarded.")]
        public string PushgatewayUrl { get; set; }

        [SeqAppSetting(
            DisplayName = "Pushgateway Counter Name",
            HelpText = "Name of the counter with which this will be identified in the Pushgateway Metrics.")]
        public string CounterName { get; set; }

        [SeqAppSetting(
            DisplayName = "Additional Property Names",
            IsOptional = true,
            HelpText = "The names of additional event properties to include in the PagerDuty incident. One per line.",
            InputType = SettingInputType.LongText)]
        public string ApplicationNameKeyList { get; set; }

        public static IMetricPushServer server;
        public readonly string instanceName = "default";

        public void On(Event<LogEventData> evt)
        {
            server = new MetricPushServer(new MetricPusher(PushgatewayUrl, CounterName, instanceName));
            var pushgatewayCounterData = FormatTemplate(evt, ApplicationNameKeyList);

            server.Start();
            var counter = Metrics.CreateCounter(CounterName, "To keep the count of no of times a particular error coming in a module.", new[] { "ApplicationName", "Message" });
            counter.Labels(pushgatewayCounterData.ResourceName, pushgatewayCounterData.RenderedMessage).Inc();
        }

        public static PushgatewayCounterData FormatTemplate(Event<LogEventData> evt, string applicationNameKeyList)
        {
            var properties = (IDictionary<string, object>)ToDynamic(evt.Data.Properties ?? new Dictionary<string, object>());

            PushgatewayCounterData data = new PushgatewayCounterData();
            data.RenderedMessage = evt.Data.RenderedMessage ?? evt.Data.MessageTemplate;

            foreach (var propertyName in applicationNameKeyList)
            {
                var name = (propertyName).ToString().Trim();
                foreach (var property in properties)
                {
                    if (property.Key == name)
                    {
                        data.ResourceName = property.Value.ToString();
                        break;
                    }
                }
            }
            return data;
        }

        private static object ToDynamic(object o)
        {
            switch (o)
            {
                case IEnumerable<KeyValuePair<string, object>> dictionary:
                    var result = new ExpandoObject();
                    var asDict = (IDictionary<string, object>)result;
                    foreach (var kvp in dictionary)
                    {
                        asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                    }
                    return result;

                case IEnumerable<object> enumerable:
                    return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }
    }
}


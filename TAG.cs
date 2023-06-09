﻿namespace TagHelperMethods
{
    using Newtonsoft.Json;
    using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
    using System.Collections.Generic;

    public class TagHelper
    {
        public static void TransitionToError(PaProfileLoadDomHelper helper, string status)
        {
            switch (status)
            {
                case "draft":
                    helper.TransitionState("draft_to_ready");
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "ready":
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "in_progress":
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "active":
                    helper.TransitionState("active_to_reprovision");
                    helper.TransitionState("reprovision_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "deactivate":
                    helper.TransitionState("deactivate_to_deactivating");
                    helper.TransitionState("deactivating_to_error");
                    break;
                case "deactivating":
                    helper.TransitionState("deactivating_to_error");
                    break;
                case "reprovision":
                    helper.TransitionState("reprovision_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "complete":
                    helper.TransitionState("complete_to_ready");
                    helper.TransitionState("ready_to_inprogress");
                    helper.TransitionState("inprogress_to_error");
                    break;
                case "active_with_errors":
                    helper.TransitionState("activewitherrors_to_deactivate");
                    helper.TransitionState("deactivate_to_deactivating");
                    helper.TransitionState("deactivating_to_error");
                    break;
            }
        }
    }

    public class TagRequest
    {
        public TagRequest()
        {
            this.ScanRequests = new List<Scan>();
            this.MonitorRequests = new List<Monitor>();
            this.MultiViewerRequests = new List<Multiviewer>();
            this.ChannelSets = new List<Channel>();
        }

        public enum TAGAction
        {
            Add = 1,
            Delete = 2,
        }

        [JsonProperty("channelSets", NullValueHandling = NullValueHandling.Ignore)]
        public List<Channel> ChannelSets { get; set; }

        [JsonProperty("monitorRequest", NullValueHandling = NullValueHandling.Ignore)]
        public List<Monitor> MonitorRequests { get; set; }

        [JsonProperty("multiviewerRequest", NullValueHandling = NullValueHandling.Ignore)]
        public List<Multiviewer> MultiViewerRequests { get; set; }

        [JsonProperty("scanRequest", NullValueHandling = NullValueHandling.Ignore)]
        public List<Scan> ScanRequests { get; set; }
    }

    public class Scan
    {
        public enum ModeState
        {
            Starting = 1,
            Running = 2,
            Canceling = 3,
            Finishing = 4,
            Finished = 5,
            Failed = 6,
            FinishedRemoved = 7,
        }

        [JsonProperty("assetId", NullValueHandling = NullValueHandling.Ignore)]
        public string AssetId { get; set; }

        [JsonProperty("action", NullValueHandling = NullValueHandling.Ignore)]
        public long? Action { get; set; }

        [JsonProperty("interface", NullValueHandling = NullValueHandling.Ignore)]
        public string Interface { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }
    }

    public class Monitor
    {
        [JsonProperty("template", NullValueHandling = NullValueHandling.Ignore)]
        public string Template { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
    }

    public class Multiviewer
    {
        [JsonProperty("event", NullValueHandling = NullValueHandling.Ignore)]
        public string Event { get; set; }

        [JsonProperty("layout", NullValueHandling = NullValueHandling.Ignore)]
        public List<Layout> Layout { get; set; }
    }

    public class Layout
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("layout", NullValueHandling = NullValueHandling.Ignore)]
        public string LayoutLayout { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("template", NullValueHandling = NullValueHandling.Ignore)]
        public string Template { get; set; }
    }

    public class Channel
    {
        [JsonProperty("template", NullValueHandling = NullValueHandling.Ignore)]
        public string Template { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public long? Id { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("thresholdSet", NullValueHandling = NullValueHandling.Ignore)]
        public string ThresholdSet { get; set; }

        [JsonProperty("notificationSet", NullValueHandling = NullValueHandling.Ignore)]
        public string NotificationSet { get; set; }

        [JsonProperty("delay", NullValueHandling = NullValueHandling.Ignore)]
        public int? Delay { get; set; }

        [JsonProperty("monitoringLevel", NullValueHandling = NullValueHandling.Ignore)]
        public int? MonitoringLevel { get; set; }

        [JsonProperty("descrambling", NullValueHandling = NullValueHandling.Ignore)]
        public int? Descrambling { get; set; }

        [JsonProperty("encryption", NullValueHandling = NullValueHandling.Ignore)]
        public string Encryption { get; set; }

        [JsonProperty("kms", NullValueHandling = NullValueHandling.Ignore)]
        public string Kms { get; set; }
    }
}
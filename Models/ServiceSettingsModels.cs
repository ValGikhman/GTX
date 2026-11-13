using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public sealed class ServiceSettingsPageModel {
        public IList<ServiceSettingsCardModel> Services { get; set; } = new List<ServiceSettingsCardModel>();
        public string InvalidServiceId { get; set; }
    }

    public sealed class ServiceSettingsCardModel {
        public string ServiceId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Style { get; set; }
        public bool Enabled { get; set; }
        public string Revision { get; set; }
        public IList<ServiceSettingsFieldModel> Fields { get; set; } = new List<ServiceSettingsFieldModel>();
    }

    public sealed class ServiceSettingsFieldModel {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }
        public int? Minimum { get; set; }
        public int? Maximum { get; set; }
        public int MaxLength { get; set; }
        public bool RequiredWhenEnabled { get; set; }
        public string Value { get; set; }
    }

    public sealed class ServiceSettingsSaveModel {
        [Required, StringLength(40)]
        public string ServiceId { get; set; }
        public bool Enabled { get; set; }
        [Required, StringLength(128)]
        public string Revision { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
    }
}

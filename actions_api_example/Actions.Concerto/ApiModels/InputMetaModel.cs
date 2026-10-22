using Actions.Concerto.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.ApiModels
{
    /// <summary>
    /// An Input Meta description returned form the Concerto API
    /// </summary>
    public class InputMetaModel
    {
        public DateTime? DateMin { get; set; }
        public DateTime? DateMax { get; set; }
        public int? IntMin { get; set; }
        public int? IntMax { get; set; }
        public string? CurrentValue { get; set; }
        public MetaItem[] AllowedValues { get; set; } = Array.Empty<MetaItem>();
    }
}

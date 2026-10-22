using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.ApiModels
{
    /// <summary>
    /// An Input description returned from the Concerto API
    /// </summary>
    public class InputModel
    {
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public InputMetaModel Meta { get; set; } = new InputMetaModel();
    }
}

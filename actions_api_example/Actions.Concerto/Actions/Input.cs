using Actions.Concerto.ApiModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.Actions
{
    internal class Input
    {
        /// <summary>
        /// The input type, as returned by 'describe'. This doubles as the path to the value within the
        /// invoke payload - dotted types such as 'Appointment.From' live in a nested object.
        /// </summary>
        internal string Type { get; set; } = string.Empty;
        internal bool IsRequired { get; set; } = true;
        internal InputMeta Meta { get; } = new InputMeta();
        internal Action<Order, Input> OnConfigure = (o, i) => { };

        /// <summary>
        /// Applies the value found at this input's path in the invoke payload to the Order.
        /// The value arrives as a JToken because inputs are not always simple scalars - some are
        /// objects or arrays.
        /// </summary>
        internal Action<JToken, Order> OnApply = (v, o) => { };

        public InputModel ToApiModel(Order order)
        {
            if (OnConfigure != null)
            {
                OnConfigure(order, this);
            }

            return new InputModel()
            {
                IsRequired = this.IsRequired,
                Type = this.Type,
                Meta = this.Meta.ToApiModel(order)
            };
        }
    }

    /// <summary>
    /// Metadata describing what a valid value for an input would look like.
    /// </summary>
    /// <remarks>
    /// Simplified for this example. The real API returns Meta as a loose JSON object whose shape
    /// varies by input type, and Appendix A of the API Guide names the shape per input -
    /// DateMeta (MustBeAfter / MustBeBefore), StringMeta (DefaultText / MaxLength),
    /// OptionSelectMeta&lt;T&gt; and OptionMultiMeta&lt;T&gt; (CurrentValue(s) and the available
    /// Options), CustomFieldMeta (the client-configured fields and their prompts), and others.
    ///
    /// The flat model here stands in for all of them: DateMin/DateMax for DateMeta, IntMin/IntMax
    /// for numeric ranges, CurrentValue for the value already held, and AllowedValues for anything
    /// option-shaped. Against the real API you would read the properties Appendix A lists for the
    /// input type in hand rather than a single fixed shape like this one.
    /// </remarks>
    internal class InputMeta
    {
        internal DateTime? DateMin { get; set; }
        internal DateTime? DateMax { get; set; }
        internal int? IntMin { get; set; }
        internal int? IntMax { get; set; }
        internal string? CurrentValue { get; set; }
        internal MetaItem[] AllowedValues { get; set; } = Array.Empty<MetaItem>();
        internal Action<Order, InputMeta> OnConfigure = (o, m) => { };

        internal InputMetaModel ToApiModel(Order order)
        {
            if (OnConfigure != null)
            {
                OnConfigure(order, this);
            }

            var meta = new InputMetaModel()
            {
                AllowedValues = this.AllowedValues,
                CurrentValue = this.CurrentValue,
                DateMax = this.DateMax,
                DateMin = this.DateMin,
                IntMax = this.IntMax,
                IntMin = this.IntMin
            };

            return meta;
        }
    }

    public class MetaItem
    {
        public string Value { get; internal set; } = string.Empty;
        public string DisplayText { get; internal set; } = string.Empty;
    }
}

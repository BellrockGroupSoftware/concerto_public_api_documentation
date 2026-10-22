using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.Actions
{
    internal class Client
    {
        public required string ClientCode { get; internal set; }
        public required string Name { get; internal set; }

        public static readonly Client Coffee = new Client { ClientCode = "coffee", Name = "Coffee Retailer" };
        public static readonly Client Food = new Client { ClientCode = "food", Name = "Food Retailer" };
        public static readonly Client LocalAuthority1 = new Client { ClientCode = "local-authority-1", Name = "Northern Council" };
        public static readonly Client LocalAuthority2 = new Client { ClientCode = "local-authority-2", Name = "Southern Council" };
    }
}

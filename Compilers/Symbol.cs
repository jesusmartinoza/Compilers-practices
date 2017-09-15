﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compilers
{
    class Symbol
    {
        private string coef;

        public Symbol(string coef)
        {
            this.coef = coef;
        }

        public bool IsTerminal()
        {
            int n;

            return coef.All(char.IsLower) || Int32.TryParse(coef, out n);
        }

        public string Coef { get => coef; set => coef = value; }
    }
}
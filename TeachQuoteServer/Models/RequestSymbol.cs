using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeachQuoteServer.Models
{
	internal class RequestSymbol
	{
		public string Symbol { get; set; }
		public string MarketNo { get; set; }
		public int StockIdx { get; set; }
	}

}

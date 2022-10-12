using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeachQuoteServer.Models
{
	[Serializable]
	public class TickPacket : IPacket
	{
		public string ID { get; set; }
		public string Symbol { get; set; }
		public double Close { get; set; }
		public int Qty { get; set; }
	}

}

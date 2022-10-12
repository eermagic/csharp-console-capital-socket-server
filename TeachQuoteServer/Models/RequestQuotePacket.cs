using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeachQuoteServer.Models
{
	[Serializable]
	public class RequestQuotePacket : IPacket
	{
		public string ID { get; set; }
		public List<string> Symbol { get; set; }

		public RequestQuotePacket(string id)
		{
			this.ID = id;
		}
	}

}

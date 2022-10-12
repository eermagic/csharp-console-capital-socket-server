using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TeachQuoteServer.Models
{
	[Serializable]
	public class ClientInfo : IPacket
	{
		public string ID { get; set; }

		[NonSerialized]
		public TcpClient Client;
		public ClientInfo(string id)
		{
			this.ID = id;
		}
	}

}

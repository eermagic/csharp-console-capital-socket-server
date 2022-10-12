using Newtonsoft.Json;
using SKCOMLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TeachQuoteServer.Models;

namespace TeachQuoteServer
{
    internal class CapitalQuote
    {
        public SKQuoteLib m_SKQuoteLib = new SKQuoteLib();// 國內報價物件
        public SKCenterLib m_pSKCenter = new SKCenterLib();// 登入&環境物件
        public SKReplyLib m_pSKReply = new SKReplyLib();// 回應物件
		double dDigitNum = 100.000; // 小數位
		public Dictionary<string, Int16> tickPageNo = new Dictionary<string, Int16>();
		int nCode;

		TcpListener? Tcp;
		bool isTcpListen = false;
		List<ClientInfo> Clients = new List<ClientInfo>();
		List<RequestSymbol> listRequestBest5 = new List<RequestSymbol>(); //訂閱商品名稱
		List<UserRquest> listUserRquest = new List<UserRquest>(); //使用者訂閱名單

		/// <summary>
		/// 建構子
		/// </summary>
		internal CapitalQuote()
        {
			m_pSKReply.OnReplyMessage += new _ISKReplyLibEvents_OnReplyMessageEventHandler(m_pSKReply_OnAnnouncement); // 註冊公告事件
			m_SKQuoteLib.OnConnection += new _ISKQuoteLibEvents_OnConnectionEventHandler(m_SKQuoteLib_OnConnection);// 註冊國內報價連線狀態事件
			m_SKQuoteLib.OnNotifyTicksLONG += new _ISKQuoteLibEvents_OnNotifyTicksLONGEventHandler(m_SKQuoteLib_OnNotifyTicks);// 國內 Tick 回傳事件
			m_SKQuoteLib.OnNotifyBest5LONG += new _ISKQuoteLibEvents_OnNotifyBest5LONGEventHandler(m_SKQuoteLib_OnNotifyBest5);// 註冊國內 Best5 回傳事件

			//群益登入帳密
			string CapitalLoginID = "XXXXXXXXXXXXXX"; //建議由設定檔讀取
			string CapitalLoginPwd = "XXXXXXXXXXXXXX"; //建議由設定檔讀取

			// 群益登入
			m_pSKCenter.SKCenterLib_SetAuthority(1);// 不用 SGX DMA

			// 登入群益帳戶
			nCode = m_pSKCenter.SKCenterLib_Login(CapitalLoginID, CapitalLoginPwd);
			if (nCode != 0 && nCode != 2003)
			{
				if (nCode == 507)
				{
					ShowMessage(this, "請檢查群益憑證安裝狀態");
				}
				ShowMessage(this, "請檢查群益憑證安裝狀態");
				GetCapitalMessage("登入", nCode);
				return;
			}
			ShowMessage(this, "群益登入成功");

			//國內報價進線
			nCode = m_SKQuoteLib.SKQuoteLib_EnterMonitorLONG();
			GetCapitalMessage("國內報價連線", nCode);

			// TCP Server
			IPEndPoint TcpEndPoint = new IPEndPoint(IPAddress.Any, 8888);
			Tcp = new TcpListener(TcpEndPoint);
			Thread ThreadTCP = new Thread(new ThreadStart(TCPListen));
			isTcpListen = true;
			ThreadTCP.Start();
		}

		/// <summary>
		/// 公告
		/// </summary>
		void m_pSKReply_OnAnnouncement(string strUserID, string bstrMessage, out short nConfirmCode)
		{
			nConfirmCode = -1;
		}

		/// <summary>
		/// 國內報價連線回應事件
		/// </summary>
		/// <param name="nKind"></param>
		/// <param name="nCode"></param>
		void m_SKQuoteLib_OnConnection(int nKind, int nCode)
		{
			if (nKind == 3001)
			{
				// 連線中
				ShowMessage(this, "連線狀態：連線中");
			}
			else if (nKind == 3002)
			{
				// 連線中斷
				ShowMessage(this, "連線狀態：中斷");
			}
			else if (nKind == 3003)
			{
				// 連線成功
				ShowMessage(this, "連線狀態：正常");
			}
			else if (nKind == 3021)
			{
				//網路斷線
				ShowMessage(this, "連線狀態：網路斷線");
			}
		}

		/// <summary>
		/// 國內 Tick 回傳事件
		/// </summary>
		void m_SKQuoteLib_OnNotifyTicks(short sMarketNo, int nStockIdx, int nPtr, int nDate, int lTimehms, int lTimemillismicros, int nBid, int nAsk, int nClose, int nQty, int nSimulate)
		{
			RequestSymbol? request = listRequestBest5.FirstOrDefault(w => w.MarketNo == sMarketNo.ToString() && w.StockIdx == nStockIdx);
			if (request != null)
			{
				List<UserRquest> items = listUserRquest.Where(w => w.Symbol == request.Symbol).ToList();
				foreach (var item in items)
				{
					TickPacket packet = new TickPacket();
					packet.Symbol = request.Symbol;
					packet.Close = nClose / dDigitNum;
					packet.Qty = nQty;

					ClientInfo? CI = Clients.FirstOrDefault(x => x.ID == item.ID);
					if (CI != null)
					{
						SendTCP(packet, CI.Client);
					}
				}
			}
		}

		/// <summary>
		/// 國內 Best5 回傳事件
		/// </summary>
		void m_SKQuoteLib_OnNotifyBest5(short sMarketNo, int nStockIdx, int nBestBid1, int nBestBidQty1, int nBestBid2, int nBestBidQty2, int nBestBid3, int nBestBidQty3, int nBestBid4, int nBestBidQty4, int nBestBid5, int nBestBidQty5, int nExtendBid, int nExtendBidQty, int nBestAsk1, int nBestAskQty1, int nBestAsk2, int nBestAskQty2, int nBestAsk3, int nBestAskQty3, int nBestAsk4, int nBestAskQty4, int nBestAsk5, int nBestAskQty5, int nExtendAsk, int nExtendAskQty, int nSimulate)
		{
			RequestSymbol? request = listRequestBest5.FirstOrDefault(w => w.MarketNo == sMarketNo.ToString() && w.StockIdx == nStockIdx);
			if (request != null)
			{
				List<UserRquest> items = listUserRquest.Where(w => w.Symbol == request.Symbol).ToList();
				foreach (var item in items)
				{
					Best5Packet packet = new Best5Packet();
					packet.Symbol = request.Symbol;
					packet.Bid1Price = nBestBid1 / dDigitNum;
					packet.Ask1Price = nBestAsk1 / dDigitNum;

					packet.Bid1Qty = nBestBidQty1;
					packet.Ask1Qty = nBestAskQty1;

					packet.Bid2Price = nBestBid2 / dDigitNum;
					packet.Bid2Qty = nBestBidQty2;
					packet.Bid3Price = nBestBid3 / dDigitNum;
					packet.Bid3Qty = nBestBidQty3;
					packet.Bid4Price = nBestBid4 / dDigitNum;
					packet.Bid4Qty = nBestBidQty4;
					packet.Bid5Price = nBestBid5 / dDigitNum;
					packet.Bid5Qty = nBestBidQty5;

					packet.Ask2Price = nBestAsk2 / dDigitNum;
					packet.Ask2Qty = nBestAskQty2;
					packet.Ask3Price = nBestAsk3 / dDigitNum;
					packet.Ask3Qty = nBestAskQty3;
					packet.Ask4Price = nBestAsk4 / dDigitNum;
					packet.Ask4Qty = nBestAskQty4;
					packet.Ask5Price = nBestAsk5 / dDigitNum;
					packet.Ask5Qty = nBestAskQty5;

					ClientInfo? CI = Clients.FirstOrDefault(x => x.ID == item.ID);
					if (CI != null)
					{
						SendTCP(packet, CI.Client);
					}
				}
			}
		}

		/// <summary>
		/// 取得群益api回傳訊息說明
		/// </summary>
		/// <param name="strType"></param>
		/// <param name="nCode"></param>
		/// <returns></returns>
		private void GetCapitalMessage(string strType, int nCode)
		{
			string strInfo = "";

			if (nCode != 0)
				strInfo = "【" + m_pSKCenter.SKCenterLib_GetLastLogInfo() + "】";

			string message = "【" + strType + "】【" + m_pSKCenter.SKCenterLib_GetReturnCodeMessage(nCode) + "】" + strInfo;
			ShowMessage(this, message);
		}

		/// <summary>
		/// 顯示訊息
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void ShowMessage(object? sender, string e)
		{
			Console.WriteLine(DateTime.Now.ToString("HH:mm:ss ") + e);
		}

		/// <summary>
		/// TCP 監聽
		/// </summary>
		private void TCPListen()
		{
			Tcp.Start();
			ShowMessage(this, "TCP Listener Started");
			while (isTcpListen)
			{
				try
				{
					TcpClient NewClient = Tcp.AcceptTcpClient();

					Action<object> ProcessData = new Action<object>(delegate (object _Client)
					{
						TcpClient Client = (TcpClient)_Client;
						Client.Client.IOControl(IOControlCode.KeepAliveValues, GetKeepAliveData(), null);
						Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

						byte[] receiveBuffer = new byte[0];
						byte[] processBuffer = new byte[0];
						byte[] packet = new byte[1024];
						byte[] lenPacket = new byte[8];
						int size = 0;
						int bytesRead = 0;
						while (Client.Connected)
						{
							try
							{
								bytesRead = Client.GetStream().Read(packet, 0, packet.Length);

								if (bytesRead > 0)
								{
									receiveBuffer = MargeByte(receiveBuffer, packet, bytesRead);
									if (receiveBuffer.Length < 8 && bytesRead < 8)
									{
										continue;
									}

									lenPacket = GetByteData(receiveBuffer, 0, 8);
									size = int.Parse(Encoding.UTF8.GetString(lenPacket));
									while (size > 0)
									{

										if (size <= receiveBuffer.Length - 8)
										{
											processBuffer = GetByteData(receiveBuffer, 8, size);
											IPacket? Item = ByteToPacket(processBuffer);
											ProcessReceive(Item, Client);

											receiveBuffer = GetByteData(receiveBuffer, 8 + size, receiveBuffer.Length - size - 8);
											if (receiveBuffer.Length < 8)
											{
												break;
											}
											lenPacket = GetByteData(receiveBuffer, 0, 8);
											size = int.Parse(Encoding.UTF8.GetString(lenPacket));
										}
										else
										{
											break;
										}
									}
								}
								else
								{
									break;
								}
							}
							catch (Exception ex)
							{
								if (ex.Message.IndexOf("遠端主機已強制關閉一個現存的連線") == -1)
								{
									ShowMessage(this, "Client TCP Error: " + ex.Message + "\n" + ex.StackTrace);
								}
								break;
							}
						}

						Disconnect(Client);
					});

					Thread ThreadProcessData = new Thread(new ParameterizedThreadStart(ProcessData));
					ThreadProcessData.Start(NewClient);
				}
				catch (Exception ex)
				{
					ShowMessage(this, "TCP Error: " + ex.Message + "\n" + ex.StackTrace);
					isTcpListen = false;
				}
			}
		}

		/// <summary>
		/// 連線心跳檢測
		/// </summary>
		/// <returns></returns>
		public byte[] GetKeepAliveData()
		{
			uint dummy = 0;
			byte[] inOptionValues = new byte[System.Runtime.InteropServices.Marshal.SizeOf(dummy) * 3];
			BitConverter.GetBytes((uint)1).CopyTo(inOptionValues, 0);
			BitConverter.GetBytes((uint)3000).CopyTo(inOptionValues, System.Runtime.InteropServices.Marshal.SizeOf(dummy));//keep-alive間隔
			BitConverter.GetBytes((uint)500).CopyTo(inOptionValues, System.Runtime.InteropServices.Marshal.SizeOf(dummy) * 2);// 嘗試間隔
			return inOptionValues;
		}

		/// <summary>
		/// 合併位元組
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="bsz"></param>
		/// <returns></returns>
		public byte[] MargeByte(byte[] a, byte[] b, int bsz)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				ms.Write(a, 0, a.Length);
				ms.Write(b, 0, (bsz == 0) ? b.Length : bsz);
				return ms.ToArray();
			}
		}

		/// <summary>
		/// 取得位元組
		/// </summary>
		/// <param name="buf"></param>
		/// <param name="pos"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public byte[] GetByteData(byte[] buf, int pos, int length)
		{
			byte[] b = new byte[length];
			Array.Copy(buf, pos, b, 0, length);
			return b;
		}

		/// <summary>
		/// Byte 轉傳送物件
		/// </summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public IPacket ByteToPacket(byte[] bytes)
		{
			string jsonString = Encoding.UTF8.GetString(bytes);
			string type = jsonString.Split('|')[0];
			try
			{
				switch (type)
				{
					case "ClientInfo":
						return JsonConvert.DeserializeObject<ClientInfo>(jsonString.Split('|')[1]);
					case "RequestQuotePacket":
						return JsonConvert.DeserializeObject<RequestQuotePacket>(jsonString.Split('|')[1]);
					case "TickPacket":
						return JsonConvert.DeserializeObject<TickPacket>(jsonString.Split('|')[1]);
					case "Best5Packet":
						return JsonConvert.DeserializeObject<Best5Packet>(jsonString.Split('|')[1]);
					default:
						throw new Exception("Not Support Type, \nSource:" + jsonString);
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Convert Error: " + ex.Message + "\nSource:" + jsonString);
			}
		}

		/// <summary>
		/// 傳送 TCP
		/// </summary>
		/// <param name="Item"></param>
		/// <param name="Client"></param>
		private void SendTCP(IPacket Item, TcpClient Client)
		{
			if (Client != null && Client.Connected)
			{
				byte[] Data = PacketToByteArray(Item);

				NetworkStream NetStream = Client.GetStream();
				NetStream.Write(Data, 0, Data.Length);
			}
		}

		/// <summary>
		/// 傳送物件轉 Byte
		/// </summary>
		/// <param name="packet"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public byte[] PacketToByteArray(IPacket packet)
		{
			string type = packet.GetType().Name;
			string jsonString = JsonConvert.SerializeObject(packet);
			byte[] data = Encoding.UTF8.GetBytes(type + "|" + jsonString);

			int len = data.Length;
			if (len > 99999999)
			{
				throw new Exception("傳送字串超過長度限制");
			}
			byte[] lenData = Encoding.UTF8.GetBytes(len.ToString("00000000"));
			byte[] newData = MargeByte(lenData, data, 0);
			return newData;
		}

		/// <summary>
		/// 處理接收項目
		/// </summary>
		/// <param name="Item"></param>
		/// <param name="Protocol"></param>
		/// <param name="EP"></param>
		/// <param name="Client"></param>
		private void ProcessReceive(IPacket Item, TcpClient Client)
		{
			if (Item.GetType() == typeof(ClientInfo))
			{
				// 使用者連線
				ClientInfo CI = Clients.FirstOrDefault(x => x.ID == ((ClientInfo)Item).ID);

				if (CI == null)
				{
					CI = (ClientInfo)Item;
					Clients.Add(CI);

					if (Client != null)
					{
						ShowMessage(this, string.Format("Client Added: ID: {0}, TCP EP: {1}:{2}", CI.ID, ((IPEndPoint)Client.Client.RemoteEndPoint).Address, ((IPEndPoint)Client.Client.RemoteEndPoint).Port));
						CI.Client = Client;
					}
				}
			}
			else if (Item.GetType() == typeof(RequestQuotePacket))
			{
				// 報價商品訂閱
				RequestQuotePacket req = (RequestQuotePacket)Item;

				int nConnected = m_SKQuoteLib.SKQuoteLib_IsConnected();
				if (nConnected == 1)
				{
					foreach (string symbol in req.Symbol)
					{
						// 加入使用者訂閱名單
						if (!listUserRquest.Any(w => w.ID == req.ID && w.Symbol == symbol))
						{
							listUserRquest.Add(new UserRquest()
							{
								ID = req.ID,
								Symbol = symbol
							});
						}

						// 訂閱報價
						if (!listRequestBest5.Any(w => w.Symbol == symbol))
						{
							// 取回商品報價的相關資訊
							SKSTOCKLONG pSKStockLONG = new SKSTOCKLONG();
							nCode = m_SKQuoteLib.SKQuoteLib_GetStockByNoLONG(symbol, ref pSKStockLONG);
							if (nCode != 0)
							{
								return;
							}
							GetCapitalMessage("訂閱商品資訊 [" + symbol + "]", nCode);

							//加入清單
							listRequestBest5.Add(new RequestSymbol()
							{
								Symbol = symbol,
								MarketNo = pSKStockLONG.bstrMarketNo,
								StockIdx = pSKStockLONG.nStockIdx
							});

							// 訂閱商品 Tick & Best5
							if (!tickPageNo.ContainsKey(symbol))
							{
								tickPageNo.Add(symbol, Convert.ToInt16(tickPageNo.Count));
							}
							//訂閱 Tick & Best5，訂閱後等待 OnNotifyTicks 及 OnNotifyBest5 事件回報
							Int16 TickPage = tickPageNo[symbol];
							nCode = m_SKQuoteLib.SKQuoteLib_RequestTicks(ref TickPage, symbol);
							GetCapitalMessage("訂閱商品 Tick & Best5 [" + symbol + "]", nCode);
						}
					}

				}
				else
				{
					ShowMessage(this, "尚未報價連線，稍後再試");
				}
			}
		}

		/// <summary>
		/// 離線
		/// </summary>
		/// <param name="Client"></param>
		private void Disconnect(TcpClient Client)
		{
			ClientInfo CI = Clients.FirstOrDefault(x => x.Client == Client);

			if (CI != null)
			{
				listUserRquest.RemoveAll(w => w.ID == CI.ID);

				Clients.Remove(CI);
				ShowMessage(this, "Client Disconnected " + Client.Client.RemoteEndPoint.ToString());
				Client.Close();

			}
		}
	}
}

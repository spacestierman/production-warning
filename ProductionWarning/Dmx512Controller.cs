using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProductionWarning
{
	class Dmx512Controller
	{
		public delegate void OnDeviceConnectedHandler();
		public event OnDeviceConnectedHandler OnDeviceConnected;

		private const int TRY_TO_FIND_DEVICE_DELAY_IN_MILLISECONDS = 100;
		private const int DATA_DELAY_IN_MILLISECONDS = 1;

		public string SerialNumber { get; private set; }

		private byte[] _buffer;
		private bool _shouldBeRunning;
		private FTDI _ftdi;

		private Thread _getDeviceThread;
		private Thread _writeDataThread;

		public TimeSpan DataRefreshInterval { get; set; }

		public Dmx512Controller()
		{
			_shouldBeRunning = false;

			_buffer = new byte[513]; // 513 is not a typo, all the examples show using a 513 length.  Maybe the first byte is reserved in the spec?
			_ftdi = new FTDI();

			DataRefreshInterval = TimeSpan.FromMilliseconds(1);
		}

		public IEnumerable<string> GetAvailableSerialNumbers()
		{
			UInt32 deviceCount = 0;
			FTDI.FT_STATUS status = _ftdi.GetNumberOfDevices(ref deviceCount);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to get the number of FTDI devices.");
			}

			FTDI.FT_DEVICE_INFO_NODE[] list = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];
			status = _ftdi.GetDeviceList(list);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to obtain the device list.");
			}

			return list.Select(x => x.SerialNumber);
		}

		public void Connect(string serialNumber)
		{
			if (string.IsNullOrEmpty(serialNumber))
			{
				throw new ArgumentException("SerialNumber is required.");
			}

			if (IsConnected)
			{
				StopThreads();
			}

			SerialNumber = serialNumber;
			StartThreads();
		}

		public void Disconnect()
		{
			StopThreads();
		}

		public void SetAddressValue(int address, int value)
		{
			if (!IsConnected)
			{
				throw new Exception("Cannot write to address values until connected.");
			}

			if (address < 1 || address > 512)
			{
				throw new ArgumentException("Address must be within [1-512].");
			}
			if (value < 0 || value > 255)
			{
				throw new ArgumentException("Value must be within [0-255].");
			}

			_buffer[address] = Convert.ToByte(value);
		}
		public void SetColorAtAddress(int startingAddress, float red, float green, float blue)
		{
			SetAddressValue(startingAddress, ConvertTo255(red));
			SetAddressValue(startingAddress + 1, ConvertTo255(green));
			SetAddressValue(startingAddress + 2, ConvertTo255(blue));
		}

		private int ConvertTo255(float value)
		{
			double converted = Math.Floor(value * 255.0);
			if (converted < 0)
			{
				return 0;
			}
			if (converted > 255)
			{
				converted = 255;
			}

			return (int)converted;
		}

		public void SetColorAtAddress(int startingAddress, int red, int green, int blue)
		{
			SetAddressValue(startingAddress, red);
			SetAddressValue(startingAddress + 1, green);
			SetAddressValue(startingAddress + 2, blue);
		}

		private void StartThreads()
		{
			_shouldBeRunning = true;

			_getDeviceThread = new Thread(new ThreadStart(GetDeviceThread));
			_getDeviceThread.Start();

			_writeDataThread = new Thread(new ThreadStart(WriteDataThread));
			_writeDataThread.Start();
		}

		private void StopThreads()
		{
			_shouldBeRunning = false;
			_getDeviceThread.Join();
			_writeDataThread.Join();

			FTDI.FT_STATUS status = _ftdi.Close();
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to close the FTDI device.");
			}
		}

		public bool IsConnected
		{
			get
			{
				return _shouldBeRunning && _ftdi.IsOpen;
			}
		}

		private void GetDeviceThread()
		{
			while (_shouldBeRunning)
			{
				while (_shouldBeRunning && !_ftdi.IsOpen)
				{
					UInt32 deviceCount = 0;
					FTDI.FT_STATUS status = _ftdi.GetNumberOfDevices(ref deviceCount);
					if (status != FTDI.FT_STATUS.FT_OK)
					{
						throw new Exception("Unable to get the number of FTDI devices.");
					}
					else
					{
						FTDI.FT_DEVICE_INFO_NODE[] list = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];
						status = _ftdi.GetDeviceList(list);
						if (status != FTDI.FT_STATUS.FT_OK)
						{
							throw new Exception("Unable to obtain the device list.");
						}
						else
						{
							FTDI.FT_DEVICE_INFO_NODE match = list.FirstOrDefault(x => x.SerialNumber == SerialNumber);
							if (match != null)
							{
								status = _ftdi.OpenBySerialNumber(match.SerialNumber);
								if (status != FTDI.FT_STATUS.FT_OK)
								{
									throw new Exception("Unable to open the FTDI device!");
								}

								InitializeDMX();

								if (OnDeviceConnected != null)
								{
									OnDeviceConnected();
								}
							}
						}
					}

					Thread.Sleep(TRY_TO_FIND_DEVICE_DELAY_IN_MILLISECONDS);
				}
			}
		}

		private void WriteDataThread()
		{
			while (_shouldBeRunning)
			{
				if (IsConnected)
				{
					WriteBufferToDevice();
					Thread.Sleep(DataRefreshInterval);
				}
			}
		}
		private void InitializeDMX()
		{
			FTDI.FT_STATUS status;

			status = _ftdi.ResetDevice();
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to reset device.");
			}

			status = _ftdi.SetBaudRate(9600);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to set baud rate on device.");
			}

			status = _ftdi.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_2, FTDI.FT_PARITY.FT_PARITY_NONE);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to set data characteristics on device.");
			}

			status = _ftdi.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_NONE, 0, 0);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to set flow control on device.");
			}

			status = _ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_TX);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to purge transmit on device.");
			}

			status = _ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX);
			if (status != FTDI.FT_STATUS.FT_OK)
			{
				throw new Exception("Unable to purge receive on device.");
			}
		}

		private void WriteBufferToDevice()
		{
			UInt32 numBytesWritten = 0;

			byte[] header = CreateDmxHeader(_buffer.Length);
			_ftdi.Write(header, header.Length, ref numBytesWritten);

			_ftdi.Write(_buffer, _buffer.Length, ref numBytesWritten);

			byte[] footer = CreateDmxEnd();
			_ftdi.Write(footer, footer.Length, ref numBytesWritten);
		}

		private byte[] CreateDmxHeader(int bufferLength)
		{
			byte[] header = new byte[4];
			header[0] = 0x7E; // DMX Start code
			header[1] = 6; // DMX transmit code
			header[2] = (byte)(bufferLength & 0xFF);
			header[3] = (byte)(bufferLength >> 8);
			return header;
		}

		private byte[] CreateDmxEnd()
		{
			byte[] endCode = new byte[1];
			endCode[0] = 0xE7;
			return endCode;
		}
	}
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common.Codec;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Reflection;

namespace PepperDash.Essentials.Devices.Common.Cameras
{
	public class CameraVisca : CameraBase, IHasCameraPtzControl, ICommunicationMonitor
	{
		public IBasicCommunication Communication { get; private set; }
		public CommunicationGather PortGather { get; private set; }

		public StatusMonitorBase CommunicationMonitor { get; private set; }

		public byte PanSpeed = 0x10;
		public byte TiltSpeed = 0x10;
		private bool IsMoving;
		private bool IsZooming;
		public bool PowerIsOn { get; private set; }

		byte[] IncomingBuffer = new byte[] { };
		public BoolFeedback PowerIsOnFeedback  { get; private set; } 

		public CameraVisca(string key, string name, IBasicCommunication comm, CameraPropertiesConfig props) :
			base(key, name)
		{

            // Default to all capabilties
            Capabilities = eCameraCapabilities.Pan | eCameraCapabilities.Tilt | eCameraCapabilities.Zoom | eCameraCapabilities.Focus; 
            
            Communication = comm;
			var socket = comm as ISocketStatus;
			if (socket != null)
			{
				// This instance uses IP control
				socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
			}
			else
			{
				// This instance uses RS-232 control
			}
			PortGather = new CommunicationGather(Communication, "\xFF");


			Communication.BytesReceived += new EventHandler<GenericCommMethodReceiveBytesArgs>(Communication_BytesReceived);
			PowerIsOnFeedback = new BoolFeedback(() => { return PowerIsOn; });

			if (props.CommunicationMonitorProperties != null)
			{
				CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, props.CommunicationMonitorProperties);
			}
			else
			{
				CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, "\x81\x09\x04\x00\xFF");
			}
			DeviceManager.AddDevice(CommunicationMonitor);


		}
		public override bool CustomActivate()
		{
			Communication.Connect();

			
			CommunicationMonitor.StatusChange += (o, a) => { Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status); };
			CommunicationMonitor.Start();


			CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			return true;
		}
		void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
		{
			Debug.Console(2, this, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());

			if (e.Client.IsConnected)
			{
				
			}
			else
			{

			}
		}
	

		void SendBytes(byte[] b)
		{
			
			if (Debug.Level == 2) // This check is here to prevent following string format from building unnecessarily on level 0 or 1
				Debug.Console(2, this, "Sending:{0}", ComTextHelper.GetEscapedText(b));

			Communication.SendBytes(b);
		}
		void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
		{
			// This is probably not thread-safe buffering
			// Append the incoming bytes with whatever is in the buffer
			var newBytes = new byte[IncomingBuffer.Length + e.Bytes.Length];
			IncomingBuffer.CopyTo(newBytes, 0);
			e.Bytes.CopyTo(newBytes, IncomingBuffer.Length);
			if (Debug.Level == 2) // This check is here to prevent following string format from building unnecessarily on level 0 or 1
				Debug.Console(2, this, "Received:{0}", ComTextHelper.GetEscapedText(newBytes));
		}


		private void SendPanTiltCommand (byte[] cmd)
		{
			var temp = new Byte[] { 0x81, 0x01, 0x06, 0x01, PanSpeed, TiltSpeed };
			int length = temp.Length + cmd.Length + 1;
			
			byte[] sum = new byte[length];
			temp.CopyTo(sum, 0);
			cmd.CopyTo(sum, temp.Length);
			sum[length - 1] = 0xFF;
			SendBytes(sum);			
		}

		public void PowerOn()
		{

			SendBytes(new Byte[] { 0x81, 0x01, 0x04, 0x00, 0x02, 0xFF });
		}

		public void PowerOff()
		{
			SendBytes(new Byte[] {0x81, 0x01, 0x04, 0x00, 0x03, 0xFF});
		}
		public void PanLeft() 
		{
			SendPanTiltCommand(new byte[] {0x01, 0x03});
			IsMoving = true;
		}
		public void PanRight() 
		{
			SendPanTiltCommand(new byte[] { 0x02, 0x03 });
			IsMoving = true;
		}
        public void PanStop()
        {
            Stop();
        }
		public void TiltDown() 
		{
			SendPanTiltCommand(new byte[] { 0x03, 0x02 });
			IsMoving = true;
		}
		public void TiltUp() 
		{
			SendPanTiltCommand(new byte[] { 0x03, 0x01 });
			IsMoving = true;
		}
        public void TiltStop()
        {
            Stop();
        }

		private void SendZoomCommand (byte cmd)
		{
			SendBytes(new byte[] {0x81, 0x01, 0x04, 0x07, cmd, 0xFF} );
		}
		public void ZoomIn() 
		{
			SendZoomCommand(0x02);
			IsZooming = true;
		}
		public void ZoomOut() 
		{
			SendZoomCommand(0x03);
			IsZooming = true;
		}
        public void ZoomStop()
        {
            Stop();
        }

		public void Stop() 
		{
			if (IsZooming)
			{
				SendZoomCommand(0x00);
				IsZooming = false;
			}
			else
			{
				SendPanTiltCommand(new byte[] {0x03, 0x03});
				IsMoving = false;
			}
		}
        public void PositionHome()
        {
            throw new NotImplementedException();
        }
		public void RecallPreset(int presetNumber)
		{
			SendBytes(new byte[] {0x81, 0x01, 0x04, 0x3F, 0x02, (byte)presetNumber, 0xFF} );
		}
		public void SavePreset(int presetNumber)
		{
			SendBytes(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, (byte)presetNumber, 0xFF });
		}

	}
}
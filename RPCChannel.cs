﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Squared.Task;
using System.Diagnostics;

namespace ShootBlues {
    public struct RPCMessage {
        public IntPtr WParam, LParam;
    }

    public class RPCChannel : NativeWindow, IDisposable {
        private int WM_RPC_MESSAGE;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private Process _Process;

        public BlockingQueue<RPCMessage> Messages = new BlockingQueue<RPCMessage>();
        public UInt32 RemoteThreadId = 0;

        public RPCChannel (Process process) 
            : base() {
            _Process = process;

            WM_RPC_MESSAGE = Win32.RegisterWindowMessage("ShootBlues.RPCMessage");
            var cp = new CreateParams {
                Caption = "ShootBlues.RPCChannel",
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
                Style = 0,
                ExStyle = WS_EX_NOACTIVATE,
                Parent = new IntPtr(-3)
            };
            CreateHandle(cp);
        }

        protected override void WndProc (ref Message m) {
            if (m.Msg == WM_RPC_MESSAGE) {
                Messages.Enqueue(new RPCMessage {
                    WParam = m.WParam,
                    LParam = m.LParam
                });
            } else {
                base.WndProc(ref m);
            }
        }

        public unsafe void Send (byte[] data) {
            if (_Process == null)
                throw new Exception("No remote process");
            if (RemoteThreadId == 0)
                throw new Exception("No remote thread");

            using (var handle = new SafeProcessHandle(
                Win32.OpenProcess(ProcessAccessFlags.All, false, _Process.Id)
            )) {
                // leaked on purpose
                var region = ProcessInjector.RemoteMemoryRegion.Allocate(_Process, handle, (uint)data.Length);

                int result;
                fixed (byte* pData = data)
                    Win32.WriteProcessMemory(
                        _Process.Handle, (uint)region.Address.ToInt64(),
                        new IntPtr(pData), region.Size,
                        out result
                    );

                Win32.PostThreadMessage(RemoteThreadId, WM_RPC_MESSAGE, region.Address, region.Size);
            }
        }

        public void Dispose () {
            DestroyHandle();
        }
    }
}
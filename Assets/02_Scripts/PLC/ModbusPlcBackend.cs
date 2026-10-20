// 프로젝트 확장용 (실제 PLC Backend 연결)

using System;
using System.Collections.Generic;

namespace LadderLab.PLC
{
    public sealed class ModbusPlcBackend : IPlcBackend
    {
        private readonly string host;
        private readonly int port;

        public ModbusPlcBackend(string targetHost = "127.0.0.1", int targetPort = 502)
        {
            host = targetHost;
            port = targetPort;
        }

        public IReadOnlyList<bool> Inputs => throw CreateNotImplementedException();
        public IReadOnlyList<bool> Outputs => throw CreateNotImplementedException();
        public IReadOnlyList<bool> InternalRelays => throw CreateNotImplementedException();

        public bool GetBit(string address)
        {
            throw CreateNotImplementedException();
        }

        public bool GetBit(PlcAddress address)
        {
            throw CreateNotImplementedException();
        }

        public void SetBit(string address, bool value)
        {
            throw CreateNotImplementedException();
        }

        public void SetBit(PlcAddress address, bool value)
        {
            throw CreateNotImplementedException();
        }

        public void ResetAll()
        {
            throw CreateNotImplementedException();
        }

        public void ResetOutputs()
        {
            throw CreateNotImplementedException();
        }

        private NotSupportedException CreateNotImplementedException()
        {
            return new NotSupportedException($"External Modbus PLC backend is planned but not implemented. Target endpoint: {host}:{port}.");
        }
    }
}

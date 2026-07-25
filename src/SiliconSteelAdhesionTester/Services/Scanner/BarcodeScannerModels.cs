using System;

namespace SiliconSteelAdhesionTester.Services.Scanner
{
    public enum BarcodeScannerSource
    {
        Oriented,
        NonOriented
    }

    public sealed class BarcodeScannerEndpoint
    {
        public BarcodeScannerEndpoint(BarcodeScannerSource source, string ipAddress, int port)
        {
            Source = source;
            IpAddress = ipAddress;
            Port = port;
        }

        public BarcodeScannerSource Source { get; }
        public string IpAddress { get; }
        public int Port { get; }
        public string DisplayName => Source == BarcodeScannerSource.Oriented ? "取向扫码枪" : "无取向扫码枪";
    }

    public sealed class BarcodeScannedEventArgs : EventArgs
    {
        public BarcodeScannedEventArgs(BarcodeScannerSource source, string barcode, DateTime scannedAt)
        {
            Source = source;
            Barcode = barcode;
            ScannedAt = scannedAt;
        }

        public BarcodeScannerSource Source { get; }
        public string Barcode { get; }
        public DateTime ScannedAt { get; }
    }

    public sealed class ScannerStatusEventArgs : EventArgs
    {
        public ScannerStatusEventArgs(BarcodeScannerSource source, bool connected, string message)
        {
            Source = source;
            Connected = connected;
            Message = message;
        }

        public BarcodeScannerSource Source { get; }
        public bool Connected { get; }
        public string Message { get; }
    }
}

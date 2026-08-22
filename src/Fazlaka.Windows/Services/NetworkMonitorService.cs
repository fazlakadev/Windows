using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fazlaka.Windows.Services;

public partial class NetworkMonitorService : ObservableObject, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private string _statusText = "متصل";

    public event EventHandler<bool>? ConnectivityChanged;

    public NetworkMonitorService()
    {
        _timer = new Timer(CheckConnectivity, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void CheckConnectivity(object? state)
    {
        if (_disposed) return;

        try
        {
            var network = NetworkInterface.GetIsNetworkAvailable();
            var wasConnected = IsConnected;

            if (network)
            {
                network = CheckInternetAccess();
            }

            if (network != wasConnected)
            {
                IsConnected = network;
                StatusText = network ? "متصل" : "غير متصل";
                ConnectivityChanged?.Invoke(this, network);
            }
        }
        catch
        {
            if (IsConnected)
            {
                IsConnected = false;
                StatusText = "غير متصل";
                ConnectivityChanged?.Invoke(this, false);
            }
        }
    }

    private static bool CheckInternetAccess()
    {
        try
        {
            using var client = new TcpClient();
            var result = client.ConnectAsync("1.1.1.1", 53).Wait(TimeSpan.FromSeconds(2));
            return result;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}

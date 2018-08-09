﻿using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

using Prism.Mvvm;

using Robock.Services;
using Robock.Shared.Win32;

namespace Robock.Models
{
    /// <summary>
    ///     Desktop is equals to Monitor, Monitor has a one Desktop.
    /// </summary>
    public class Desktop : BindableBase, IDisposable
    {
        private readonly RobockClient _client;
        private readonly Screen _screen;
        private readonly string _uuid;
        private IDisposable _disposable;
        private Process _process;

        public int No { get; }
        public bool IsPrimary => _screen.Primary;
        public double X => _screen.Bounds.X;
        public double Y => _screen.Bounds.Y;
        public double Height => _screen.Bounds.Height;
        public double Width => _screen.Bounds.Width;

        public Desktop(Screen screen, int index)
        {
            _screen = screen;
            No = index;
            _uuid = $"Background.Desktop{index}";
            _client = new RobockClient(_uuid);
        }

        public void Dispose()
        {
            try
            {
                Close().Wait();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public async Task Handshake(Action action = null)
        {
            _process = Process.Start("Robock.Background.exe", $"{_uuid}");
            if (_process == null)
                return;
            _process.WaitForInputIdle();

            var offsetX = (SystemParameters.VirtualScreenLeft < 0 ? -1 : 1) * SystemParameters.VirtualScreenLeft;
            var offsetY = (SystemParameters.VirtualScreenTop < 0 ? -1 : 1) * SystemParameters.VirtualScreenTop;

            await _client.Handshake((int) (offsetX + X), (int) (offsetY + Y), (int) Height, (int) Width);

            // 生存確認
            _disposable = Observable.Timer(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)).Subscribe(async _ => await Heartbeat());
        }

        public async Task ApplyWallpaper(IntPtr hWnd, RECT rect)
        {
            await _client.ApplyWallpaper(hWnd, rect);
            IsConnecting = true;
        }

        public async Task DiscardWallpaper()
        {
            await _client.DiscardWallpaper();
            await Close();
            IsConnecting = false;
        }

        private async Task Heartbeat()
        {
            try
            {
                await _client.Heartbeat();
            }
            catch (Exception)
            {
                StatusTextService.Instance.Status = "Heartbeat failed, disconnect from background process.";
                if (_process == null)
                    return;
                _disposable.Dispose();
                _process.WaitForExit();
                _process.Dispose();
                IsConnecting = false;
            }
        }

        private async Task Close()
        {
            StatusTextService.Instance.Status = "Waiting for shutting down of background process...";
            await _client.Close();
            if (_process == null)
                return;

            _disposable?.Dispose();
            _process.CloseMainWindow();
            _process.WaitForExit();
            _process.Dispose();
            StatusTextService.Instance.Status = "Shutdown successful";
        }

        #region IsConnecting

        private bool _isConnecting;

        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        #endregion
    }
}
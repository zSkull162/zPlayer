using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace zPlayer.App.Playback
{
    public sealed record AudioOutputDevice(string? Id, string Name);

    public static class AudioOutputDevices
    {
        public static List<AudioOutputDevice> GetDevices() {
            List<AudioOutputDevice> devices = [
                new(null, "Windows default")
            ];

            using MMDeviceEnumerator enumerator = new();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) {
                using (device) devices.Add(new(device.ID, device.FriendlyName));
            }
            return devices;
        }

        public static MMDevice Resolve(string? deviceId) {
            using MMDeviceEnumerator enumerator = new();

            if (!string.IsNullOrWhiteSpace(deviceId)) {
                MMDevice? device = null;
                try {
                    device = enumerator.GetDevice(deviceId);
                    if (device.State == DeviceState.Active) return device;
                }
                catch (COMException) { }
                device?.Dispose();
            }
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
    }
}
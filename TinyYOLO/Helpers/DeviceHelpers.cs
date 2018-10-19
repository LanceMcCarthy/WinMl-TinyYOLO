using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace TinyYOLO.Helpers
{
    public static class DeviceHelpers
    {
        public static async Task<DeviceInformation> FindBestCameraAsync()
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            Debug.WriteLine($"{devices.Count} devices found");

            // If there are no cameras connected to the device
            if (devices.Count == 0)
                return null;

            // If there is only one camera, return that one
            if (devices.Count == 1)
                return devices.FirstOrDefault();

            //check if the preferred device is available
            var frontCamera = devices.FirstOrDefault(
                x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);

            //if front camera is available return it, otherwise pick the first available camera
            return frontCamera ?? devices.FirstOrDefault();
        }
    }
}

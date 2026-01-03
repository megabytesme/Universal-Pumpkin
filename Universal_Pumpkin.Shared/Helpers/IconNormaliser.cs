using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

using Windows.Storage.Streams;

public static class IconNormaliser
{

    public static async Task NormaliseAndWriteIconAsync(byte[] inputBytes, StorageFolder targetFolder, string fileName)
    {
        if (inputBytes == null || inputBytes.Length == 0)
            throw new ArgumentException("Input image data is empty.", nameof(inputBytes));

        StorageFile file = await targetFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {

            using (var inputStream = new InMemoryRandomAccessStream())
            {
                await inputStream.WriteAsync(inputBytes.AsBuffer());
                inputStream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                var transform = new BitmapTransform
                {
                    ScaledWidth = 64,
                    ScaledHeight = 64,
                    InterpolationMode = BitmapInterpolationMode.Fant
                };

                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    64,
                    64,
                    decoder.DpiX,
                    decoder.DpiY,
                    pixelData.DetachPixelData());

                await encoder.FlushAsync();
            }
        }
    }
}
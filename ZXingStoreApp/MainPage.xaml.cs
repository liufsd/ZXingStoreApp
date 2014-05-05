using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641
using ZXing;

namespace ZXingStoreApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly MediaCapture _mediaCapture = new MediaCapture();
        private Result _result;
        private bool _navBack;
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            InitCameraPreviewAction();
        }

        private async void InitCameraPreviewAction()
        {
#if _TestCatpureImage_
  await DecodeStaticResource();
            return;
#endif
            var canUseCamera = true;
            try
            {
                var cameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (cameras.Count < 1)
                {
                    Error.Text = "No camera found, decoding static image";
                    await DecodeStaticResource();
                    return;
                }
                MediaCaptureInitializationSettings settings;
                if (cameras.Count == 1)
                {
                    settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameras[0].Id }; // 0 => front, 1 => back
                }
                else
                {
                    settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameras[1].Id }; // 0 => front, 1 => back
                }
                await _mediaCapture.InitializeAsync(settings);
                SetResolution();
                //set flash close
                _mediaCapture.VideoDeviceController.FlashControl.Enabled = false;
                VideoCapture.Source = _mediaCapture;
                try
                {
                    //should update CurrentOrientation
                    _mediaCapture.SetPreviewRotation(VideoRotationLookup(DisplayProperties.CurrentOrientation, false));
                }
                catch (Exception e)
                {
                    App.WpLog(e);
                }

                await _mediaCapture.StartPreviewAsync();
                var count = 0;
                while (_result == null && !_navBack)
                {
                    count++;
                    App.WpLog(count);
                    var photoStorageFile = await KnownFolders.PicturesLibrary.CreateFileAsync("scan.jpg", CreationCollisionOption.GenerateUniqueName);
                    await _mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), photoStorageFile);

                    var stream = await photoStorageFile.OpenReadAsync();
                    // initialize with 1,1 to get the current size of the image
                    var writeableBmp = new WriteableBitmap(1, 1);
                    writeableBmp.SetSource(stream);
                    // and create it again because otherwise the WB isn't fully initialized and decoding
                    // results in a IndexOutOfRange
                    writeableBmp = new WriteableBitmap(writeableBmp.PixelWidth, writeableBmp.PixelHeight);
                    stream.Seek(0);
                    writeableBmp.SetSource(stream);

                    _result = ScanBitmap(writeableBmp);

                    await photoStorageFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                await _mediaCapture.StopPreviewAsync();
                VideoCapture.Visibility = Visibility.Collapsed;
                CaptureImage.Visibility = Visibility.Visible;
                ScanResult.Text = _result.Text;
            }
            catch (Exception ex)
            {
                Error.Text = ex.Message;
                App.WpLog("use camera fail: " + ex);
                canUseCamera = false;
            }
            if (canUseCamera || _navBack) return;
          //tips error 
        }

        private async System.Threading.Tasks.Task DecodeStaticResource()
        {
#if _TestCatpureImage_
            var file = await KnownFolders.PicturesLibrary.GetFileAsync("scan_test.jpg");
            var image = new BitmapImage();
            image.SetSource(await file.OpenAsync(FileAccessMode.Read));
            CaptureImage.Source = image;
            CaptureImage.Visibility = Visibility.Visible; 
            return;
#else
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(@"Assets\TestZXing.png");
            var stream = await file.OpenReadAsync();
            // initialize with 1,1 to get the current size of the image
            var writeableBmp = new WriteableBitmap(1, 1);
            writeableBmp.SetSource(stream);
            // and create it again because otherwise the WB isn't fully initialized and decoding
            // results in a IndexOutOfRange
            writeableBmp = new WriteableBitmap(writeableBmp.PixelWidth, writeableBmp.PixelHeight);
            stream.Seek(0);
            writeableBmp.SetSource(stream);
            CaptureImage.Source = writeableBmp;
            VideoCapture.Visibility = Visibility.Collapsed;
            CaptureImage.Visibility = Visibility.Visible;

            _result = ScanBitmap(writeableBmp);
            if (_result != null)
            {
                ScanResult.Text += _result.Text;
            }
            return;
#endif
        }

        private Result ScanBitmap(WriteableBitmap writeableBmp)
        {
            var barcodeReader = new BarcodeReader
            {
                TryHarder = true,
                AutoRotate = true
            };
            var result = barcodeReader.Decode(writeableBmp);

            if (result != null)
            {
                CaptureImage.Source = writeableBmp;
            }

            return result;
        }

        private Windows.Media.Capture.VideoRotation VideoRotationLookup(
         Windows.Graphics.Display.DisplayOrientations displayOrientation,
         bool counterclockwise)
        {
            switch (displayOrientation)
            {
                case Windows.Graphics.Display.DisplayOrientations.Landscape:
                    return Windows.Media.Capture.VideoRotation.None;

                case Windows.Graphics.Display.DisplayOrientations.Portrait:
                    return (counterclockwise) ? Windows.Media.Capture.VideoRotation.Clockwise270Degrees :
                        Windows.Media.Capture.VideoRotation.Clockwise90Degrees;

                case Windows.Graphics.Display.DisplayOrientations.LandscapeFlipped:
                    return Windows.Media.Capture.VideoRotation.Clockwise180Degrees;

                case Windows.Graphics.Display.DisplayOrientations.PortraitFlipped:
                    return (counterclockwise) ? Windows.Media.Capture.VideoRotation.Clockwise90Degrees :
                        Windows.Media.Capture.VideoRotation.Clockwise270Degrees;

                default:
                    return Windows.Media.Capture.VideoRotation.None;
            }
        }
        //This is how you can set your resolution
        public async void SetResolution()
        {
            System.Collections.Generic.IReadOnlyList<IMediaEncodingProperties> res;
            res = this._mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
            uint maxResolution = 0;
            int indexMaxResolution = 0;

            if (res.Count >= 1)
            {
                for (int i = 0; i < res.Count; i++)
                {
                    var vp = (VideoEncodingProperties)res[i];
                    App.WpLog(vp.Width + " _ " + vp.Height);
                    if (vp.Width > maxResolution)
                    {
                        indexMaxResolution = i;
                        maxResolution = vp.Width;
                        Debug.WriteLine("Resolution: " + vp.Width);
                    }
                }
                await this._mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, res[res.Count - 1]);
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            try
            {
                _navBack = true;
                VideoCapture.Visibility = Visibility.Collapsed;
                await _mediaCapture.StopPreviewAsync();
                VideoCapture.Source = null;
            }
            catch (Exception exception)
            {
                App.WpLog(exception);
            }
        }

    }
}

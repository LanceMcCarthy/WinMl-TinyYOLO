using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using TinyYOLO.Models;

namespace TinyYOLO.Views
{
    public sealed partial class MainPage : Page
    {
        private const string ModelFilename = "TinyYOLO.onnx";

        private readonly SolidColorBrush _lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private readonly SolidColorBrush _fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        private readonly double _lineThickness = 2.0;        

        private ImageVariableDescriptorPreview _inputImageDescription;
        private TensorVariableDescriptorPreview _outputTensorDescription;
        private LearningModelPreview _model;

        private IList<YoloBoundingBox> _boxes = new List<YoloBoundingBox>();
        private readonly YoloWinMlParser _parser = new YoloWinMlParser();
        
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void ButtonRun_Click(object sender, RoutedEventArgs e)
        {
            ButtonRun.IsEnabled = false;
            
            try
            {
                // Load the model
                await Task.Run(async () => await LoadModelAsync());

                // Trigger file picker to select an image file
                var fileOpenPicker = new FileOpenPicker {SuggestedStartLocation = PickerLocationId.PicturesLibrary};
                fileOpenPicker.FileTypeFilter.Add(".jpg");
                fileOpenPicker.FileTypeFilter.Add(".png");
                fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

                var selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();

                SoftwareBitmap softwareBitmap;

                using (IRandomAccessStream stream = await selectedStorageFile.OpenAsync(FileAccessMode.Read))
                {
                    // Create the decoder from the stream 
                    var decoder = await BitmapDecoder.CreateAsync(stream);

                    // Get the SoftwareBitmap representation of the file in BGRA8 format
                    softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                // Encapsulate the image within a VideoFrame to be bound and evaluated
                var inputImage = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);

                await Task.Run(async () =>
                {
                    // Evaluate the image
                    await EvaluateVideoFrameAsync(inputImage);
                });

                this.OverlayCanvas.Children.Clear();
                
                if (this._boxes.Count > 0)
                {
                    // Filter out 
                    var filteredBoxes = this._parser.NonMaxSuppress(this._boxes, 5, .5F);

                    foreach (var box in filteredBoxes)
                    {
                        await this.DrawYoloBoundingBoxAsync(inputImage.SoftwareBitmap, box);
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"error: {ex.Message}");
                ButtonRun.IsEnabled = true;
            }
        }

        private async Task LoadModelAsync()
        {
            if (this._model != null)
                return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"Loading { ModelFilename } ... patience ");

            try
            {
                // Load Model
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/{ ModelFilename }"));
                this._model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);

                // Retrieve model input and output variable descriptions (we already know the model takes an image in and outputs a tensor)
                var inputFeatures = this._model.Description.InputFeatures.ToList();
                var outputFeatures = this._model.Description.OutputFeatures.ToList();

                this._inputImageDescription = inputFeatures.FirstOrDefault(f => f.ModelFeatureKind == LearningModelFeatureKindPreview.Image)
                    as ImageVariableDescriptorPreview;

                this._outputTensorDescription = outputFeatures.FirstOrDefault(f => f.ModelFeatureKind == LearningModelFeatureKindPreview.Tensor)
                    as TensorVariableDescriptorPreview;
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"error: {ex.Message}");
                _model = null;
            }
        }

        private async Task EvaluateVideoFrameAsync(VideoFrame inputFrame)
        {
            if (inputFrame != null)
            {
                try
                {
                    // Create bindings for the input and output buffer
                    var binding = new LearningModelBindingPreview(this._model as LearningModelPreview);

                    // R4 WinML does needs the output pre-allocated for multi-dimensional tensors
                    var outputArray = new List<float>(); 
                    outputArray.AddRange(new float[21125]);  // Total size of TinyYOLO output

                    binding.Bind(this._inputImageDescription.Name, inputFrame);
                    binding.Bind(this._outputTensorDescription.Name, outputArray);

                    // Process the frame with the model
                    var results = await this._model.EvaluateAsync(binding, "TinyYOLO");
                    var resultProbabilities = 
                        results.Outputs[this._outputTensorDescription.Name] as List<float>;

                    // Use out helper to parse to the YOLO outputs into bounding boxes with labels
                    this._boxes = this._parser.ParseOutputs(resultProbabilities.ToArray(), .3F);

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = "Model Evaluation Completed");
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"error: {ex.Message}");
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => ButtonRun.IsEnabled = true);
            }
        }

        private async Task DrawYoloBoundingBoxAsync(SoftwareBitmap inputImage, YoloBoundingBox box)
        {
            // Scale is set to stretched 416x416 - Clip bounding boxes to image area
            var x = (uint)Math.Max(box.X, 0);
            var y = (uint)Math.Max(box.Y, 0);
            var w = (uint)Math.Min(this.OverlayCanvas.Width - x, box.Width);
            var h = (uint)Math.Min(this.OverlayCanvas.Height - y, box.Height);

            var brush = new ImageBrush {Stretch = Stretch.Fill};

            var bitmapSource = new SoftwareBitmapSource();
            await bitmapSource.SetBitmapAsync(inputImage);

            brush.ImageSource = bitmapSource;

            this.OverlayCanvas.Background = brush;
            
            this.OverlayCanvas.Children.Add(new Rectangle
            {
                Width = 134,
                Height = 29,
                Fill = this._lineBrush,
                Margin = new Thickness(x, y, 0, 0)
            });

            this.OverlayCanvas.Children.Add(new TextBlock
            {
                Margin = new Thickness(x + 4, y + 4, 0, 0),
                Text = $"{box.Label} ({Math.Round(box.Confidence, 4).ToString(CultureInfo.InvariantCulture)})",
                FontWeight = FontWeights.Bold,
                Width = 126,
                Height = 21,
                HorizontalTextAlignment = TextAlignment.Center
            });

            this.OverlayCanvas.Children.Add(new Rectangle
            {
                Tag = box,
                Width = w,
                Height = h,
                Fill = this._fillBrush,
                Stroke = this._lineBrush,
                StrokeThickness = this._lineThickness,
                Margin = new Thickness(x, y, 0, 0)
            });
        }
    }
}

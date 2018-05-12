using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using TinyYOLO.VideoEffects.Helpers;
using TinyYOLO.VideoEffects.Models;

namespace TinyYOLO.VideoEffects
{
    public sealed class TinyYoloVideoEffect : IBasicVideoEffect
    {
        // ** Fields ** //

        // Video Effect Fields
        private VideoEncodingProperties currentEncodingProperties;
        private CanvasDevice canvasDevice;
        private IPropertySet currentConfiguration;

        // WinML Fields
        private LearningModelPreview model;
        private ImageVariableDescriptorPreview inputImageDescription;
        private TensorVariableDescriptorPreview outputTensorDescription;
        private YoloWinMlParser parser;
        private IList<YoloBoundingBox> filteredBoxes;

        // General
        private bool isLoadingModel;
        private readonly TimeSpan poolTimerInterval = TimeSpan.FromSeconds(2);
        private ThreadPoolTimer frameProcessingTimer;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private VideoFrame evaluatableVideoFrame;

        // ** Properties ** //
        
        /// <summary>
        /// The path for the model file
        /// </summary>
        public Uri ModelUri { get; set; } = new Uri("ms-appx:///Assets/TinyYOLO.onnx");
        
        
        // ** Methods ** //

        // This is run for every video frame passed in the media pipleine (MediaPlayer, MediaCapture, etc)
        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            evaluatableVideoFrame = VideoFrame.CreateWithDirect3D11Surface(context.InputFrame.Direct3DSurface);
            
            // ********** Draw Bounding Boxes with Win2D ********** //

            // Use Direct3DSurface if using GPU memory
            if (context.InputFrame.Direct3DSurface != null)
            {
                using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
                using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(inputBitmap);

                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(renderTarget.Bounds.Width - x, box.Width);
                        var h = (uint)Math.Min(renderTarget.Bounds.Height - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }
                
                return;
            }

            // Use SoftwareBitmap if using CPU memory
            if (context.InputFrame.SoftwareBitmap != null)
            {
                // InputFrame's raw pixels
                byte[] inputFrameBytes = new byte[4 * context.InputFrame.SoftwareBitmap.PixelWidth * context.InputFrame.SoftwareBitmap.PixelHeight];
                context.InputFrame.SoftwareBitmap.CopyToBuffer(inputFrameBytes.AsBuffer());

                using (var inputBitmap = CanvasBitmap.CreateFromBytes(canvasDevice, inputFrameBytes, context.InputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight,context.InputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat()))
                using (var renderTarget = new CanvasRenderTarget(canvasDevice, context.OutputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight, (float) context.OutputFrame.SoftwareBitmap.DpiX, context.OutputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat(), CanvasAlphaMode.Premultiplied))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(inputBitmap);

                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelWidth - x, box.Width);
                        var h = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelHeight - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }
            }
        }

        // This method is executed by the ThreadPoolTimer, it performs the evaluation on a copy of the VideoFrame
        private async void EvaluateVideoFrame(ThreadPoolTimer timer)
        {
            // If a lock is being held, or WinML isn't fully initialized, return
            if (!semaphore.Wait(0) || isLoadingModel)
                return;

            try
            {
                using (evaluatableVideoFrame)
                {
                    // ************ WinML Evaluate Frame ************ //

                    Debug.WriteLine($"RelativeTime in Seconds: {evaluatableVideoFrame.RelativeTime?.Seconds}");

                    var binding = new LearningModelBindingPreview(model); // Create bindings for the input and output buffer
                    var outputArray = new List<float>(); // R4 WinML does needs the output pre-allocated for multi-dimensional tensors
                    outputArray.AddRange(new float[21125]);  // Total size of TinyYOLO output

                    binding.Bind(inputImageDescription.Name, evaluatableVideoFrame);
                    binding.Bind(outputTensorDescription.Name, outputArray);

                    // Need to figure out a way to make this work here Task.Run or similar, that only updates the value of 
                    // filteredBoxes when this is done
                    var results = await model.EvaluateAsync(binding, "TinyYOLO");

                    var resultProbabilities = results.Outputs[outputTensorDescription.Name] as List<float>;

                    // Use out helper to parse to the YOLO outputs into bounding boxes with labels
                    var boxes = parser.ParseOutputs(resultProbabilities?.ToArray());

                    // Remove overlapping and low confidence bounding boxes
                    filteredBoxes = parser.NonMaxSuppress(boxes, 5, .5F);

                    Debug.WriteLine(filteredBoxes.Count <= 0 ? $"No Valid Bounding Boxes" : $"Valid Bounding Boxes: {filteredBoxes.Count}");

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EvaluateFrameException: {ex}");
            }
            finally
            {
                semaphore.Release();
            }
        }


        // Loads the ML model file and sets 
        private async Task LoadModelAsync()
        {
            if (model != null)
                return;

            Debug.WriteLine($"Loading Model");

            isLoadingModel = true;

            try
            {
                // Load Model
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(ModelUri);
                Debug.WriteLine($"Model file discovered at: {modelFile.Path}");

                model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);
                Debug.WriteLine($"LearningModelPReview object instantiated: {modelFile.Path}");

                // Retrieve model input and output variable descriptions (we already know the model takes an image in and outputs a tensor)
                var inputFeatures = model.Description.InputFeatures.ToList();
                Debug.WriteLine($"{inputFeatures.Count} Input Features");

                var outputFeatures = model.Description.OutputFeatures.ToList();
                Debug.WriteLine($"{inputFeatures.Count} Output Features");

                inputImageDescription = inputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Image)
                    as ImageVariableDescriptorPreview;

                outputTensorDescription = outputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Tensor)
                    as TensorVariableDescriptorPreview;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading model: {ex.Message}");
                model = null;
            }
            finally
            {
                isLoadingModel = false;
            }
        }

        
        // ********** IBasicVideoEffect Requirements ********** //

        public void SetProperties(IPropertySet configuration)
        {
            currentConfiguration = configuration;
        }

        public async void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            currentEncodingProperties = encodingProperties;

            filteredBoxes = new List<YoloBoundingBox>();

            frameProcessingTimer = ThreadPoolTimer.CreatePeriodicTimer(EvaluateVideoFrame, poolTimerInterval);

            canvasDevice = device != null ? CanvasDevice.CreateFromDirect3D11Device(device) : CanvasDevice.GetSharedDevice();

            parser = new YoloWinMlParser();

            await LoadModelAsync();
        }

        public void Close(MediaEffectClosedReason reason)
        {
            canvasDevice?.Dispose();
            semaphore?.Dispose();
            frameProcessingTimer?.Cancel();
        }
        
        public MediaMemoryTypes SupportedMemoryTypes => EffectConstants.SupportedMemoryTypes;

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties => EffectConstants.SupportedEncodingProperties;
        
        public bool IsReadOnly => false;
        public bool TimeIndependent => false;
        public void DiscardQueuedFrames() { }
    }
}
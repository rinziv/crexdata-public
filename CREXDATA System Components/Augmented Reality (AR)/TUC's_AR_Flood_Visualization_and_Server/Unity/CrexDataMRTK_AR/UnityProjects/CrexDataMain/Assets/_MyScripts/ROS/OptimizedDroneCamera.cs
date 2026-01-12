using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using NaughtyAttributes;
using System.Threading;
using UnityEngine.UI;

public class OptimizedDroneCamera : MonoBehaviour
{
    public enum DisplayType { Renderer, RawImage }

    [Header("Display Settings")]
    [SerializeField]
    private DisplayType displayType = DisplayType.Renderer;
    [SerializeField]
    [Tooltip("Assign the UI RawImage component here if using the RawImage display type.")]
    private RawImage rawImageComponent, rawImageComponent2;
    [SerializeField]
    [Tooltip("Texture to display when no signal is received.")]
    private Texture2D noSignalTexture;
    [SerializeField]
    [Tooltip("Time in seconds before showing the 'No Signal' texture.")]
    private float timeout = 2.0f;

    private ROSConnection ros;
    private Texture2D texture;
    private Renderer rendererComponent; // Renamed to avoid conflict with base.renderer

    // ROS Settings
    [SerializeField]
    private string compressedTopicName = "/camera/color/image_raw/compressed";
    [SerializeField]
    private string rawTopicName = "/camera/color/image_raw";
    [SerializeField]
    private bool flipRawImageVertically = true;

    // Threading and Data Handling for Raw Images
    private Thread _imageProcessingThread;
    private volatile bool _isRunning = false;
    private readonly object _dataLock = new();

    // Data from ROS thread, passed to worker thread
    private ImageMsg _latestRawImageMsg;
    private AutoResetEvent _newRawImageAvailableSignal = new(false);

    // Data from worker thread, passed to main thread
    private byte[] _processedTextureData;
    private uint _processedTextureWidth;
    private uint _processedTextureHeight;
    private TextureFormat _processedTextureFormat;
    private volatile bool _isProcessedTextureReady = false;

    // For compressed images (handled on main thread for simplicity, as LoadImage is efficient)
    private CompressedImageMsg _latestCompressedImageMsg;
    private volatile bool _isCompressedImageReady = false;

    private float lastMessageTime;
    private bool isShowingNoSignal = true;

    void Start()
    {
        if (displayType == DisplayType.Renderer)
        {
            rendererComponent = GetComponent<Renderer>();
            if (rendererComponent == null)
            {
                Debug.LogError("DisplayType is set to Renderer, but no Renderer component was found on the GameObject.", this);
                this.enabled = false;
                return;
            }
        }
        else if (displayType == DisplayType.RawImage)
        {
            if (rawImageComponent == null || rawImageComponent2 == null)
            {
                Debug.LogError("DisplayType is set to RawImage, but the RawImage component has not been assigned in the Inspector.", this);
                enabled = false;
                return;
            }
        }

        texture = new Texture2D(2, 2);

        ShowNoSignalTexture();
        isShowingNoSignal = true;

        _isRunning = true;
        _imageProcessingThread = new Thread(ImageProcessingWorkerLoop);
        _imageProcessingThread.IsBackground = true; // Ensure thread exits when application quits
        _imageProcessingThread.Start();
    }

    public void SubscribeToTopics()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<CompressedImageMsg>(compressedTopicName, ReceiveCompressedImageCallback);
        ros.Subscribe<ImageMsg>(rawTopicName, ReceiveRawImageCallback);
        Debug.Log($"Subscribed to ROS topics: {compressedTopicName}, {rawTopicName}");
    }
    public void UnsubscribeFromTopics()
    {
        if (ros != null)
        {
            ros.Unsubscribe(compressedTopicName);
            ros.Unsubscribe(rawTopicName);
            Debug.Log($"Unsubscribed from ROS topics: {compressedTopicName}, {rawTopicName}");
        }
    }
    void ReceiveCompressedImageCallback(CompressedImageMsg compressedImageMsg)
    {
        lock (_dataLock)
        {
            _latestCompressedImageMsg = compressedImageMsg;
            _isCompressedImageReady = true;
            lastMessageTime = Time.time;
        }
    }

    void ReceiveRawImageCallback(ImageMsg rawImageMsg)
    {
        // This is called on a ROS thread
        lock (_dataLock)
        {
            // Store the latest message. If the worker is busy, it will pick up the newest one available.
            _latestRawImageMsg = rawImageMsg;
            lastMessageTime = Time.time;
        }
        _newRawImageAvailableSignal.Set(); // Signal the worker thread
    }

    private void ImageProcessingWorkerLoop()
    {
        while (_isRunning)
        {
            _newRawImageAvailableSignal.WaitOne(); // Wait for a signal
            if (!_isRunning) break;

            ImageMsg imageToProcess;
            lock (_dataLock)
            {
                if (_latestRawImageMsg == null) continue; // Should not happen if signaled
                imageToProcess = _latestRawImageMsg;
                _latestRawImageMsg = null; // Consume the message
            }

            if (imageToProcess == null) continue;

            // Perform the heavy processing
            byte[] rawData = imageToProcess.data;
            uint width = imageToProcess.width;
            uint height = imageToProcess.height;
            string encoding = imageToProcess.encoding.ToLower();
            TextureFormat unityFormat;

            byte[] finalPixelData = ProcessRawImageData(rawData, width, height, encoding, out unityFormat);

            if (finalPixelData != null)
            {
                lock (_dataLock)
                {
                    _processedTextureData = finalPixelData;
                    _processedTextureWidth = width;
                    _processedTextureHeight = height;
                    _processedTextureFormat = unityFormat;
                    _isProcessedTextureReady = true;
                }
            }
        }
    }

    private byte[] ProcessRawImageData(byte[] imageData, uint width, uint height, string encoding, out TextureFormat unityFormat)
    {
        unityFormat = TextureFormat.RGB24; // Default
        bool requiresBGRConversion = false;
        bool isMonoToRGB = false;

        if (encoding == "rgb8") unityFormat = TextureFormat.RGB24;
        else if (encoding == "rgba8") unityFormat = TextureFormat.RGBA32;
        else if (encoding == "bgr8") { unityFormat = TextureFormat.RGB24; requiresBGRConversion = true; }
        else if (encoding == "bgra8") { unityFormat = TextureFormat.RGBA32; requiresBGRConversion = true; }
        else if (encoding == "mono8") { unityFormat = TextureFormat.RGB24; isMonoToRGB = true; }
        else
        {
            Debug.LogWarning($"Unsupported image encoding: {encoding}. Attempting RGB24.");
            // Fallback or handle error
            return null;
        }

        byte[] currentData = imageData;

        if (requiresBGRConversion)
        {
            currentData = ConvertPixelOrder(currentData, (int)width, (int)height, encoding);
        }
        else if (isMonoToRGB)
        {
            currentData = ConvertMono8ToRGB24(currentData, (int)width, (int)height);
        }

        if (flipRawImageVertically)
        {
            int bytesPerPixel = 3; // Default for RGB24
            if (unityFormat == TextureFormat.RGBA32) bytesPerPixel = 4;
            // Note: if mono8 was directly supported by a TextureFormat (e.g. R8), bpp would be 1.
            // But since we convert mono8 to RGB24, bpp is 3 for the flipped data.
            currentData = FlipImageVertically(currentData, (int)width, (int)height, bytesPerPixel);
        }
        return currentData;
    }


    void Update()
    {
        // Timeout check
        if (Time.time - lastMessageTime > timeout && !isShowingNoSignal)
        {
            ShowNoSignalTexture();
            isShowingNoSignal = true;
        }

        // Handle Compressed Images (Main Thread)
        if (_isCompressedImageReady)
        {
            CompressedImageMsg compressedMsgToProcess = null;
            lock (_dataLock)
            {
                if (_latestCompressedImageMsg != null)
                {
                    compressedMsgToProcess = _latestCompressedImageMsg;
                    _latestCompressedImageMsg = null;
                    _isCompressedImageReady = false;
                }
            }
            if (compressedMsgToProcess != null)
            {
                if (isShowingNoSignal)
                {
                    AssignVideoTexture();
                    isShowingNoSignal = false;
                }
                texture.LoadImage(compressedMsgToProcess.data); // LoadImage handles Apply implicitly
            }
        }

        // Handle Processed Raw Images (Main Thread)
        if (_isProcessedTextureReady)
        {
            byte[] textureDataToApply = null;
            uint texWidth = 0;
            uint texHeight = 0;
            TextureFormat texFormat = TextureFormat.RGB24;

            lock (_dataLock)
            {
                if (_processedTextureData != null)
                {
                    textureDataToApply = _processedTextureData;
                    texWidth = _processedTextureWidth;
                    texHeight = _processedTextureHeight;
                    texFormat = _processedTextureFormat;

                    _processedTextureData = null; // Allow GC for this array after we've copied its reference
                    _isProcessedTextureReady = false;
                }
            }

            if (textureDataToApply != null)
            {
                if (isShowingNoSignal)
                {
                    AssignVideoTexture();
                    isShowingNoSignal = false;
                }

                if (texture.width != texWidth || texture.height != texHeight || texture.format != texFormat)
                {
                    texture.Reinitialize((int)texWidth, (int)texHeight, texFormat, false);
                }
                texture.LoadRawTextureData(textureDataToApply);
                texture.Apply();
            }
        }
    }

    void ShowNoSignalTexture()
    {
        if (noSignalTexture != null)
        {
            if (displayType == DisplayType.Renderer && rendererComponent != null)
            {
                rendererComponent.material.mainTexture = noSignalTexture;
            }
            else if (displayType == DisplayType.RawImage && rawImageComponent != null)
            {
                rawImageComponent.texture = noSignalTexture;
                rawImageComponent2.texture = noSignalTexture;
            }
        }
    }

    void AssignVideoTexture()
    {
        if (displayType == DisplayType.Renderer && rendererComponent != null)
        {
            rendererComponent.material.mainTexture = texture;
        }
        else if (displayType == DisplayType.RawImage && rawImageComponent != null && rawImageComponent2 != null)
        {
            if (rawImageComponent.gameObject.activeSelf)
            {
                rawImageComponent.texture = texture;
                print($"Assigning");
            }
            if (rawImageComponent2.gameObject.activeSelf)
                rawImageComponent2.texture = texture;
        }
    }

    byte[] ConvertMono8ToRGB24(byte[] mono8Data, int width, int height)
    {
        byte[] rgbData = new byte[width * height * 3];
        for (int i = 0; i < width * height; i++)
        {
            byte monoValue = mono8Data[i];
            rgbData[i * 3 + 0] = monoValue;
            rgbData[i * 3 + 1] = monoValue;
            rgbData[i * 3 + 2] = monoValue;
        }
        return rgbData;
    }

    byte[] ConvertPixelOrder(byte[] pixelData, int width, int height, string encoding)
    {
        byte[] convertedData = new byte[pixelData.Length];
        int channels = (encoding == "bgr8" || encoding == "rgb8") ? 3 : 4;

        for (int i = 0; i < width * height; i++)
        {
            convertedData[i * channels + 0] = pixelData[i * channels + 2]; // R <-> B
            convertedData[i * channels + 1] = pixelData[i * channels + 1]; // G
            convertedData[i * channels + 2] = pixelData[i * channels + 0]; // B <-> R
            if (channels == 4)
            {
                convertedData[i * channels + 3] = pixelData[i * channels + 3]; // A
            }
        }
        return convertedData;
    }

    byte[] FlipImageVertically(byte[] imageData, int width, int height, int bytesPerPixel)
    {
        byte[] flippedData = new byte[imageData.Length];
        int stride = width * bytesPerPixel;
        for (int y = 0; y < height; y++)
        {
            System.Array.Copy(imageData, y * stride, flippedData, (height - 1 - y) * stride, stride);
        }
        return flippedData;
    }

    void OnDestroy()
    {
        _isRunning = false;
        if (_newRawImageAvailableSignal != null)
        {
            _newRawImageAvailableSignal.Set(); // Wake up thread if it's waiting, so it can see _isRunning is false
        }
        if (_imageProcessingThread != null && _imageProcessingThread.IsAlive)
        {
            _imageProcessingThread.Join(500); // Wait for the thread to finish, with a timeout
        }
        if (_newRawImageAvailableSignal != null)
        {
            _newRawImageAvailableSignal.Close();
            _newRawImageAvailableSignal = null;
        }

        // Unsubscribe from ROS topics if ROSConnection instance is still valid
        if (ros != null && ROSConnection.GetOrCreateInstance() != null)
        {
            ROSConnection.GetOrCreateInstance().Unsubscribe(compressedTopicName);
            ROSConnection.GetOrCreateInstance().Unsubscribe(rawTopicName);
        }

        if (texture != null) Destroy(texture);
    }

    [Button("Clear Texture (Optimized)")]
    [System.Obsolete("ClearTexture is deprecated. Texture clears on new data or can be manually reset if needed.")]
    public void ClearTexture()
    {
        if (texture != null)
        {
            // Reinitialize to a small transparent texture
            texture.Reinitialize(2, 2, TextureFormat.RGBA32, false);
            Color32[] resetPixels = new Color32[4]; // Transparent
            for (int i = 0; i < resetPixels.Length; ++i) resetPixels[i] = new Color32(0, 0, 0, 0);
            texture.SetPixels32(resetPixels);
            texture.Apply();
        }
        else
        {
            Debug.LogWarning("Texture is null, cannot clear.");
        }
        // Clear pending processed data
        lock (_dataLock)
        {
            _processedTextureData = null;
            _isProcessedTextureReady = false;
            _latestCompressedImageMsg = null;
            _isCompressedImageReady = false;
        }
    }
}
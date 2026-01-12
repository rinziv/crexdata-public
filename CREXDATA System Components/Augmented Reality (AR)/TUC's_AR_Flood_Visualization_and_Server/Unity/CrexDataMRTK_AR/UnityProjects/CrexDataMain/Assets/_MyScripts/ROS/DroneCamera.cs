using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using NaughtyAttributes;
using System;

public class DroneCamera : MonoBehaviour
{
    private Texture2D texture;
    private new Renderer renderer;
    private bool isCompressedMessageReceived = false;
    private bool isRawMessageReceived = false;

    // Variables to store data from ROS thread for raw images
    private byte[] rawImageData;
    private uint rawImageWidth;
    private uint rawImageHeight;
    private string rawImageEncoding;
    private readonly object rawImageLock = new object();


    [SerializeField]
    private string compressedTopicName = "/camera/color/image_raw/compressed";
    [SerializeField]
    private string rawTopicName = "/camera/color/image_raw"; // Example raw topic
    [SerializeField]
    private bool flipRawImageVertically = true; // Add this to control flipping


    void Start()
    {
        var ros = ROSConnection.GetOrCreateInstance();

        // Choose one subscription or implement logic to switch/use both
        // Example: Subscribing to the compressed topic by default
        ros.Subscribe<CompressedImageMsg>(compressedTopicName, ReceiveCompressedImage);

        // To subscribe to the raw image topic instead (or in addition):
        ros.Subscribe<ImageMsg>(rawTopicName, ReceiveRawImageCallback);

        renderer = GetComponent<Renderer>();
        //texture = new Texture2D(2, 2);
        renderer.material.mainTexture = texture;
    }

    // Callback for CompressedImageMsg
    void ReceiveCompressedImage(CompressedImageMsg compressedImageMsg)
    {
        // texture.LoadImage handles JPEG/PNG decoding and resizing
        texture.LoadImage(compressedImageMsg.data);
        isCompressedMessageReceived = true; // Flag that a compressed image was processed
    }

    // Callback for ImageMsg (raw image data)
    // This method is called from a ROS thread.
    void ReceiveRawImageCallback(ImageMsg rawImageMsg)
    {
        lock (rawImageLock)
        {
            rawImageData = rawImageMsg.data;
            rawImageWidth = rawImageMsg.width;
            rawImageHeight = rawImageMsg.height;
            rawImageEncoding = rawImageMsg.encoding.ToLower();
            isRawMessageReceived = true;
        }
    }

    void Update()
    {
        if (isCompressedMessageReceived)
        {
            // For compressed images, LoadImage usually handles Apply().
            // If updates are not visible, you might need texture.Apply() here.
            // However, LoadImage is quite efficient.
            isCompressedMessageReceived = false;
        }

        if (isRawMessageReceived)
        {
            ProcessRawImage();
            isRawMessageReceived = false;
        }
    }

    // Process raw image data in the main Unity thread
    private void ProcessRawImage()
    {
        byte[] currentImageData;
        uint currentWidth;
        uint currentHeight;
        string currentEncoding;

        lock (rawImageLock)
        {
            currentImageData = rawImageData;
            currentWidth = rawImageWidth;
            currentHeight = rawImageHeight;
            currentEncoding = rawImageEncoding; // Already toLower() from callback
        }

        if (currentImageData == null || currentImageData.Length == 0) return;

        //Debug.Log($"Processing raw image. Encoding: '{currentEncoding}', Width: {currentWidth}, Height: {currentHeight}");

        TextureFormat format = TextureFormat.RGB24; // Default
        bool requiresConversion = false;
        bool isMonoToRGB = false; // Flag for mono8 to RGB24 conversion

        if (currentEncoding == "rgb8") format = TextureFormat.RGB24;
        else if (currentEncoding == "rgba8") format = TextureFormat.RGBA32;
        else if (currentEncoding == "bgr8") { format = TextureFormat.RGB24; requiresConversion = true; }
        else if (currentEncoding == "bgra8") { format = TextureFormat.RGBA32; requiresConversion = true; }
        else if (currentEncoding == "mono8")
        {
            format = TextureFormat.RGB24; // We will convert mono8 to RGB24
            isMonoToRGB = true;
        }
        else
        {
            //Debug.LogWarning($"Unsupported image encoding: {currentEncoding}. Attempting to display as RGB24 without conversion.");
            format = TextureFormat.RGB24;
        }

        //Debug.Log($"Chosen format: {format}, RequiresConversion: {requiresConversion}, IsMonoToRGB: {isMonoToRGB}");

        if (texture.width != currentWidth || texture.height != currentHeight || texture.format != format)
        {
            //Debug.Log($"Reinitializing texture. Old: {texture.width}x{texture.height} {texture.format}. New: {currentWidth}x{currentHeight} {format}");
            texture.Reinitialize((int)currentWidth, (int)currentHeight, format, false);
        }

        byte[] processedImageData = currentImageData;
        if (requiresConversion)
        {
            //Debug.Log("Performing BGR/BGRA pixel order conversion.");
            processedImageData = ConvertPixelOrder(currentImageData, (int)currentWidth, (int)currentHeight, currentEncoding);
        }
        else if (isMonoToRGB)
        {
            //Debug.Log("Performing mono8 to RGB24 conversion.");
            processedImageData = ConvertMono8ToRGB24(currentImageData, (int)currentWidth, (int)currentHeight);
        }

        // Flip the image vertically if needed
        if (flipRawImageVertically)
        {
            //Debug.Log("Flipping image vertically.");
            // Determine bytesPerPixel for the *final* processedImageData before flipping
            // This depends on the format the data is in *after* conversions
            int finalBytesPerPixel = 3; // Default to RGB24
            if (format == TextureFormat.RGBA32) finalBytesPerPixel = 4;
            else if (format == TextureFormat.RGB24) finalBytesPerPixel = 3;
            // Add other formats if you support them directly without converting to RGB24/RGBA32
            // For example, if you were to use TextureFormat.R8 for mono8 directly:
            // else if (format == TextureFormat.R8) finalBytesPerPixel = 1;

            processedImageData = FlipImageVertically(processedImageData, (int)currentWidth, (int)currentHeight, finalBytesPerPixel);
        }

        texture.LoadRawTextureData(processedImageData);
        texture.Apply();
    }

    byte[] ConvertMono8ToRGB24(byte[] mono8Data, int width, int height)
    {
        byte[] rgbData = new byte[width * height * 3]; // 3 bytes per pixel (R, G, B)
        for (int i = 0; i < width * height; i++)
        {
            byte monoValue = mono8Data[i];
            rgbData[i * 3 + 0] = monoValue; // R
            rgbData[i * 3 + 1] = monoValue; // G
            rgbData[i * 3 + 2] = monoValue; // B
        }
        return rgbData;
    }

    // Helper function to convert BGR/BGRA to RGB/RGBA
    byte[] ConvertPixelOrder(byte[] pixelData, int width, int height, string encoding)
    {
        byte[] convertedData = new byte[pixelData.Length];
        int channels = 0;
        if (encoding == "bgr8") channels = 3;
        else if (encoding == "bgra8") channels = 4;
        else return pixelData; // Should not happen if requiresConversion is true

        for (int i = 0; i < width * height; i++)
        {
            convertedData[i * channels + 0] = pixelData[i * channels + 2]; // R
            convertedData[i * channels + 1] = pixelData[i * channels + 1]; // G
            convertedData[i * channels + 2] = pixelData[i * channels + 0]; // B
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
            int srcRowStartIndex = y * stride;
            int destRowStartIndex = (height - 1 - y) * stride;
            System.Array.Copy(imageData, srcRowStartIndex, flippedData, destRowStartIndex, stride);
        }
        return flippedData;
    }

    [Button("Clear Texture")]
    public void ClearTexture()
    {
        if (texture != null)
        {
            texture.Reinitialize(2, 2);
            texture.Apply();
        }
        else
        {
            Debug.LogWarning("Texture is null, cannot clear.");
        }
    }
}
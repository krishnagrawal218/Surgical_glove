
using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;
using UnityEngine;

public class InputReader : MonoBehaviour
{
    public HandController handController;
    public string portName = "COM3";
    public int baudRate = 115200;

    [Range(0, 1)] public float thumb;
    [Range(0, 1)] public float index;
    [Range(0, 1)] public float middle;
    [Range(0, 1)] public float ring;
    [Range(0, 1)] public float little;

    public bool connected;

    SerialPort sp;
    Thread readThread;
    volatile bool running;

    readonly object dataLock = new object();

    int[] raw = new int[5];
    int[] straight = new int[5];
    int[] bent = new int[5];

    bool hasData, straightSet, bentSet;
    string status = "Waiting for ESP32";

    void Start()
    {
        if (handController == null)
        {
            status = "Assign HandController!";
            Debug.LogError(status);
            return;
        }

        try
        {
            sp = new SerialPort(portName, baudRate);
            sp.ReadTimeout = 100;
            sp.NewLine = "\n";
            sp.Open();

            running = true;
            readThread = new Thread(ReadData);
            readThread.IsBackground = true;
            readThread.Start();

            connected = true;
            status = "Keep fingers straight and calibrate";
        }
        catch (Exception e)
        {
            status = "Connection failed: " + e.Message;
            Debug.LogError(status);
        }
    }

    void ReadData()
    {
        while (running)
        {
            try
            {
                string[] v = sp.ReadLine().Trim().Split(',');

                if (v.Length != 10 || v[0] != "D")
                    continue;

                int[] f = new int[5];
                bool valid = true;

                for (int i = 0; i < 5; i++)
                {
                    if (!int.TryParse(v[i + 1], out f[i]))
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                    continue;

                lock (dataLock)
                {
                    raw = f;
                    hasData = true;
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning("Serial error: " + e.Message);
                break;
            }
        }
    }

    void Update()
    {
        int[] f;
        bool ready;

        lock (dataLock)
        {
            f = (int[])raw.Clone();
            ready = hasData;
        }

        if (!ready || handController == null)
            return;

        float[] values = new float[5];

        for (int i = 0; i < 5; i++)
        {
            int range = bent[i] - straight[i];

            if (straightSet && bentSet && Mathf.Abs(range) > 10)
                values[i] = Mathf.Clamp01(
                    (float)(f[i] - straight[i]) / range
                );
        }

        thumb = values[0];
        index = values[1];
        middle = values[2];
        ring = values[3];
        little = values[4];

        handController.fingers[0].bendInput = thumb;
        handController.fingers[1].bendInput = index;
        handController.fingers[2].bendInput = middle;
        handController.fingers[3].bendInput = ring;
        handController.fingers[4].bendInput = little;
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 215), "SurgiSync");

        GUI.Label(new Rect(20, 40, 280, 25),
            connected ? "ESP32: Connected" : "ESP32: Disconnected");

        GUI.Label(new Rect(20, 65, 280, 35), status);

        string label = !straightSet ? "Capture Straight Hand" :
            !bentSet ? "Capture Bent Hand" : "Recalibrate";

        if (GUI.Button(new Rect(20, 105, 270, 35), label))
        {
            int[] f;
            bool ready;

            lock (dataLock)
            {
                f = (int[])raw.Clone();
                ready = hasData;
            }

            if (!ready)
            {
                status = "No sensor data received yet.";
                return;
            }

            if (!straightSet || bentSet)
            {
                straight = f;
                straightSet = true;
                bentSet = false;
                status = "Straight captured. Bend all fingers.";
            }
            else
            {
                bool valid = true;

                for (int i = 0; i < 5; i++)
                {
                    if (Mathf.Abs(f[i] - straight[i]) < 50)
                        valid = false;
                }

                if (valid)
                {
                    bent = f;
                    bentSet = true;
                    status = "Calibration complete!";
                }
                else
                {
                    status = "Insufficient movement. Retry.";
                }
            }
        }

        GUI.Label(new Rect(20, 150, 280, 55),
            $"Thumb {thumb:F2}  Index {index:F2}\n" +
            $"Middle {middle:F2}  Ring {ring:F2}  Little {little:F2}");
    }

    void OnDestroy()
    {
        running = false;

        if (sp != null)
        {
            try
            {
                if (sp.IsOpen)
                    sp.Close();

                sp.Dispose();
            }
            catch (Exception) { }
        }

        if (readThread != null && readThread.IsAlive)
            readThread.Join(200);
    }
}

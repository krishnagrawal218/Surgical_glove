
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

    public float pitch;
    public float roll;
    public bool mpuCalibrated;
    public bool connected;

    public float neutralPitch;
    public float neutralRoll;

    public bool isRecording;
    public long timestampMs;

    SerialPort sp;
    Thread readThread;
    volatile bool running;

    readonly object dataLock = new object();

    int[] raw = new int[5];
    int[] straight = new int[5];
    int[] bent = new int[5];

    float rawPitch, rawRoll;
    bool hasData, straightSet;
    bool[] bentSet = new bool[5];

    bool rawRecording;
    long rawTimestampMs;

    string status = "Waiting for ESP32";

    string[] names = {
        "Thumb", "Index", "Middle", "Ring", "Little"
    };

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
            status = "Capture straight hand first";
        }
        catch (Exception e)
        {
            connected = false;
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

                if (v.Length != 11 || v[0] != "D")
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

                float p, r;

                if (!float.TryParse(v[6],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture, out p))
                    continue;

                if (!float.TryParse(v[7],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture, out r))
                    continue;

                int rec;
                long ms;

                if (!int.TryParse(v[9], out rec))
                    continue;

                if (!long.TryParse(v[10], out ms))
                    continue;

                lock (dataLock)
                {
                    raw = f;
                    rawPitch = p;
                    rawRoll = r;
                    rawRecording = rec == 1;
                    rawTimestampMs = ms;
                    hasData = true;
                }
            }
            catch (TimeoutException) { }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning("Serial error: " + e.Message);

                connected = false;
                break;
            }
        }
    }

    void Update()
    {
        int[] f;
        float p, r;
        bool ready;
        bool rec;
        long ms;

        lock (dataLock)
        {
            f = (int[])raw.Clone();
            p = rawPitch;
            r = rawRoll;
            rec = rawRecording;
            ms = rawTimestampMs;
            ready = hasData;
        }

        if (!ready || handController == null)
            return;

        pitch = p;
        roll = r;
        isRecording = rec;
        timestampMs = ms;

        float[] values = new float[5];

        for (int i = 0; i < 5; i++)
        {
            int range = bent[i] - straight[i];

            if (straightSet && bentSet[i] &&
                Mathf.Abs(range) > 10)
            {
                values[i] = Mathf.Clamp01(
                    (float)(f[i] - straight[i]) / range
                );
            }
        }

        thumb = values[0];
        index = values[1];
        middle = values[2];
        ring = values[3];
        little = values[4];

        if (handController.fingers != null &&
            handController.fingers.Length >= 5)
        {
            for (int i = 0; i < 5; i++)
            {
                if (handController.fingers[i] != null)
                    handController.fingers[i].bendInput = values[i];
            }
        }
    }

    void CaptureStraight()
    {
        int[] f;

        lock (dataLock)
        {
            if (!hasData)
            {
                status = "No sensor data!";
                return;
            }

            f = (int[])raw.Clone();
        }

        straight = f;
        straightSet = true;

        for (int i = 0; i < 5; i++)
            bentSet[i] = false;

        status = "Straight saved. Calibrate each finger.";
    }

    void CaptureBent(int i)
    {
        if (!straightSet)
        {
            status = "Capture straight hand first!";
            return;
        }

        int[] f;

        lock (dataLock)
        {
            if (!hasData)
            {
                status = "No sensor data!";
                return;
            }

            f = (int[])raw.Clone();
        }

        bent[i] = f[i];
        bentSet[i] = true;

        status = names[i] + " calibrated!";
    }

    void CalibrateMPU()
    {
        lock (dataLock)
        {
            if (!hasData)
            {
                status = "No sensor data!";
                return;
            }

            neutralPitch = rawPitch;
            neutralRoll = rawRoll;
            mpuCalibrated = true;
        }

        status = "MPU neutral position saved!";
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 330, 510), "SurgiSync");

        GUI.Label(new Rect(20, 40, 300, 25),
            connected ? "ESP32: Connected" : "ESP32: Disconnected");

        GUI.Label(new Rect(20, 65, 300, 30), status);

        GUI.Label(new Rect(20, 95, 300, 30),
            isRecording ? "RECORDING: YES" : "RECORDING: NO");

        GUI.Label(new Rect(20, 120, 300, 25),
            "Timestamp: " + timestampMs + " ms");

        if (GUI.Button(new Rect(20, 150, 290, 32),
            "Capture Straight Hand"))
            CaptureStraight();

        for (int i = 0; i < 5; i++)
        {
            string label = bentSet[i]
                ? names[i] + " - Calibrated"
                : "Capture " + names[i] + " Bent";

            if (GUI.Button(
                new Rect(20, 190 + i * 35, 290, 30), label))
                CaptureBent(i);
        }

        if (GUI.Button(new Rect(20, 370, 290, 35),
            "Calibrate MPU (Hold Neutral)"))
            CalibrateMPU();

        GUI.Label(new Rect(20, 410, 300, 40),
            $"Pitch: {pitch:F1}  Roll: {roll:F1}\n" +
            $"Neutral: {neutralPitch:F1}, {neutralRoll:F1}");

        GUI.Label(new Rect(20, 455, 300, 50),
            $"T:{thumb:F2} I:{index:F2} M:{middle:F2}\n" +
            $"R:{ring:F2} L:{little:F2}");
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

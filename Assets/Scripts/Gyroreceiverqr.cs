using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System;
using TMPro;

/// <summary>
/// 電腦端 — QR Code 連線版
///
/// 【設置步驟】
/// 1. 安裝 QRCoder：
///    - 到 https://github.com/codebude/QRCoder/releases 下載 QRCoder.dll（.NET Standard 2.0）
///    - 或用 NuGet：nuget install QRCoder -Version 1.4.3
///    - 將 QRCoder.dll 放到 Assets/Plugins/ 資料夾
///
/// 2. 建立 UI：
///    - Canvas (Screen Space - Overlay)
///      ├─ Panel（半透明黑底）
///      │   ├─ RawImage（命名 QRCodeImage，建議 256x256）
///      │   ├─ Text（命名 IPLabel）
///      │   └─ Text（命名 StatusText）
///
/// 3. 將此腳本掛到 GameManager，並把上述 UI 元件拖入 Inspector
/// </summary>
public class GyroReceiverQR : MonoBehaviour
{
    [Header("網路設定")]
    public int dataPort = 9999;

    [Header("箱子設定")]
    public Transform boxTransform;
    public float sensitivity = 80f;
    public float smoothSpeed = 8f;
    public KeyCode resetKey = KeyCode.R;

    [Header("QR Code UI")]
    public RawImage qrCodeImage;     // 顯示 QR Code 的 RawImage
    public GameObject qrPanel;       // QR Code 面板（連線後可隱藏）
    public TMP_Text ipLabel;             // 顯示 IP 文字（給不方便掃描的人）
    public TMP_Text statusText;          // 狀態文字

    // ---------- 旋轉 ----------
    private Vector3 targetEuler;
    private Vector3 currentEuler;

    // ---------- 網路 ----------
    private UdpClient dataUdp;
    private Thread receiveThread;
    private bool running;

    // ---------- 執行緒安全 ----------
    private Vector3 latestGyro;
    private readonly object dataLock = new object();
    private bool hasNewData;
    private string connectedPhone = "";
    private bool justConnected = false;

    void Start()
    {
        if (boxTransform == null)
        {
            var go = GameObject.Find("Box") ?? GameObject.Find("Cube");
            if (go) boxTransform = go.transform;
        }

        string localIP = GetLocalIPAddress();
        string qrContent = $"GYRO:{localIP}:{dataPort}";

        // 顯示 IP 文字
        if (ipLabel != null)
            ipLabel.text = $"{localIP}:{dataPort}";

        // 生成並顯示 QR Code
        GenerateAndShowQR(qrContent);

        // 啟動 UDP 接收
        running = true;
        dataUdp = new UdpClient(dataPort);
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();

        SetStatus("掃描 QR Code 以連線手機");
        Debug.Log($"[QR] 內容：{qrContent}");
    }

    // ── QR Code 生成（使用 QRCoder 函式庫）──────────────────────
    void GenerateAndShowQR(string content)
    {
        if (qrCodeImage == null) return;

        var qrGenerator = new QRCoder.QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.M);
        var qrCode = new UnityQRCode(qrData);

        Texture2D tex = qrCode.GetGraphic(10);
        tex.filterMode = FilterMode.Point;
        qrCodeImage.texture = tex;
    }

    // ── UDP 接收 ────────────────────────────────────────────────
    void ReceiveLoop()
    {
        var anyEP = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] bytes = dataUdp.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(bytes);
                ParseGyro(msg, anyEP.Address.ToString());
            }
            catch (SocketException) { }
            catch (Exception e) { Debug.LogWarning("[接收] " + e.Message); }
        }
    }

    void ParseGyro(string msg, string senderIP)
    {
        try
        {
            Vector3 gyro = Vector3.zero;
            foreach (var part in msg.Split('|'))
            {
                if (part.StartsWith("gyro:"))
                {
                    var v = part.Substring(5).Split(',');
                    gyro = new Vector3(float.Parse(v[0]), float.Parse(v[1]), float.Parse(v[2]));
                }
            }
            lock (dataLock)
            {
                latestGyro = gyro;
                hasNewData = true;
                if (connectedPhone != senderIP)
                {
                    connectedPhone = senderIP;
                    justConnected = true;
                }
            }
        }
        catch { }
    }

    // ── Update ──────────────────────────────────────────────────
    void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            targetEuler = currentEuler = Vector3.zero;
            if (boxTransform) boxTransform.localRotation = Quaternion.identity;
        }

        Vector3 gyro;
        bool newData, newConn;
        string phone;

        lock (dataLock)
        {
            gyro = latestGyro;
            newData = hasNewData;
            newConn = justConnected;
            phone = connectedPhone;
            hasNewData = false;
            justConnected = false;
        }

        // 第一次收到數據 → 隱藏 QR Panel
        if (newConn)
        {
            if (qrPanel != null) qrPanel.SetActive(false);
            SetStatus($"已連線：{phone}");
        }

        if (newData)
        {
            targetEuler.x += gyro.x * sensitivity * Time.deltaTime;
            targetEuler.y += gyro.y * sensitivity * Time.deltaTime;
            targetEuler.z += gyro.z * sensitivity * Time.deltaTime;
        }

        currentEuler = Vector3.Lerp(currentEuler, targetEuler, smoothSpeed * Time.deltaTime);
        if (boxTransform)
            boxTransform.localRotation = Quaternion.Euler(currentEuler);
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
        Debug.Log("[狀態] " + msg);
    }

    static string GetLocalIPAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                string ip = ua.Address.ToString();
                if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                    return ip;
            }
        }
        return "127.0.0.1";
    }

    void OnDestroy()
    {
        running = false;
        dataUdp?.Close();
        receiveThread?.Abort();
    }
}
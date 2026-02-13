using UnityEngine;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class AutoGyroReceiver : MonoBehaviour
{
    [Header("方塊設定")]
    public Transform cube;
    public float rotationSpeed = 5f; // 旋轉平滑度

    [Header("UI")]
    public Text statusText;

    [Header("網路設定")]
    public int dataPort = 8888;      // 接收陀螺儀數據
    public int discoveryPort = 8889; // 自動發現

    private UdpClient dataServer;
    private UdpClient discoveryServer;
    private Thread dataThread;
    private Thread discoveryThread;

    private Vector3 gyroData = Vector3.zero;
    private Quaternion targetRotation = Quaternion.identity;

    private bool isReceiving = false;
    private string connectedPhone = "";
    private int packetsReceived = 0;

    void Start()
    {
        string localIP = GetLocalIPAddress();
        UpdateStatus($"等待手機連接...\n\n本機IP: {localIP}\n\n手機開啟App後\n會自動連接!");

        StartServers();
    }

    void StartServers()
    {
        // 啟動自動發現伺服器
        StartDiscoveryServer();

        // 啟動數據接收伺服器
        StartDataServer();
    }

    void StartDiscoveryServer()
    {
        try
        {
            discoveryServer = new UdpClient(discoveryPort);
            discoveryServer.EnableBroadcast = true;

            discoveryThread = new Thread(new ThreadStart(HandleDiscovery));
            discoveryThread.IsBackground = true;
            discoveryThread.Start();

            Debug.Log($"自動發現伺服器已啟動,監聽端口: {discoveryPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"啟動發現伺服器失敗: {e.Message}");
        }
    }

    void HandleDiscovery()
    {
        while (true)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = discoveryServer.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                if (message == "GYRO_DISCOVER")
                {
                    Debug.Log($"發現手機: {remoteEndPoint.Address}");

                    // 回應手機
                    string response = "GYRO_SERVER";
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    discoveryServer.Send(responseData, responseData.Length, remoteEndPoint);

                    connectedPhone = remoteEndPoint.Address.ToString();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"發現處理錯誤: {e.Message}");
            }
        }
    }

    void StartDataServer()
    {
        try
        {
            dataServer = new UdpClient(dataPort);
            dataThread = new Thread(new ThreadStart(ReceiveData));
            dataThread.IsBackground = true;
            dataThread.Start();

            Debug.Log($"數據伺服器已啟動,監聽端口: {dataPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"啟動數據伺服器失敗: {e.Message}");
        }
    }

    void ReceiveData()
    {
        while (true)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = dataServer.Receive(ref remoteEndPoint);
                string message = Encoding.UTF8.GetString(data);

                string[] values = message.Split(',');
                if (values.Length == 3)
                {
                    gyroData = new Vector3(
                        float.Parse(values[0]),
                        float.Parse(values[1]),
                        float.Parse(values[2])
                    );

                    if (!isReceiving)
                    {
                        isReceiving = true;
                        connectedPhone = remoteEndPoint.Address.ToString();
                        Debug.Log($"手機已連接: {connectedPhone}");
                    }

                    packetsReceived++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"接收錯誤: {e.Message}");
            }
        }
    }

    void Update()
    {
        if (isReceiving && cube != null)
        {
            // 根據陀螺儀數據計算旋轉
            // gravity.x = 左右傾斜 (-1到1)
            // gravity.y = 前後傾斜 (-1到1)

            float tiltX = gyroData.x * 90f; // 左右傾斜角度
            float tiltZ = gyroData.y * 90f; // 前後傾斜角度

            // 計算目標旋轉
            targetRotation = Quaternion.Euler(tiltZ, 0, -tiltX);

            // 平滑旋轉
            cube.rotation = Quaternion.Lerp(cube.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // 更新UI
            UpdateStatus($"✓ 已連接手機\n{connectedPhone}\n\n接收封包: {packetsReceived}");

            UpdateStatus($"陀螺儀:\n" +
                       $"X: {gyroData.x:F2}\n" +
                       $"Y: {gyroData.y:F2}\n\n" +
                       $"方塊旋轉:\n" +
                       $"X: {cube.rotation.eulerAngles.x:F1}°\n" +
                       $"Y: {cube.rotation.eulerAngles.y:F1}°\n" +
                       $"Z: {cube.rotation.eulerAngles.z:F1}°");
        }
    }

    string GetLocalIPAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"獲取IP失敗: {e.Message}");
        }
        return "未知";
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    void OnApplicationQuit()
    {
        if (dataThread != null) dataThread.Abort();
        if (discoveryThread != null) discoveryThread.Abort();
        if (dataServer != null) dataServer.Close();
        if (discoveryServer != null) discoveryServer.Close();
    }
}
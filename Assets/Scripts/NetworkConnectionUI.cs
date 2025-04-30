using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
#if NEW_INPUT_SYSTEM_INSTALLED
using UnityEngine.InputSystem.UI;
#endif

namespace Unity.Multiplayer.Center.NetcodeForGameObjectsExample
{
    public class NetworkConnectionUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button m_StartHostButton;
        [SerializeField] private Button m_StartClientButton;
        [SerializeField] private TMP_InputField m_IpAddressInput;
        [SerializeField] private TMP_InputField m_PortInput;
        [SerializeField] private Button m_DisconnectButton;
        [SerializeField] private TextMeshProUGUI m_ConnectionStatusText;
        [SerializeField] private GameObject StartScreen;
        [SerializeField] private GameObject roundsOjbect;
        [SerializeField] private GameObject AddPlayerButton;

        // Default values
        private string m_DefaultIp = "127.0.0.1";
        private ushort m_DefaultPort = 7777;
        private UnityTransport m_Transport;
        private NetworkManager m_NetworkManager;

        private void Awake()
        {
            // Create an EventSystem if none exists
            if (!FindAnyObjectByType<EventSystem>())
            {
                var inputType = typeof(StandaloneInputModule);
#if ENABLE_INPUT_SYSTEM && NEW_INPUT_SYSTEM_INSTALLED
                inputType = typeof(InputSystemUIInputModule);                
#endif
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
                eventSystem.transform.SetParent(transform);
                EventSystem.current.SetSelectedGameObject(m_StartHostButton.gameObject);
            }
        }

        private void Start()
        {
            m_NetworkManager = GetComponent<NetworkManager>();
            
            if (m_NetworkManager == null)
            {
                m_NetworkManager = FindFirstObjectByType<NetworkManager>();
                if (m_NetworkManager == null)
                {
                    Debug.LogError("NetworkManager not found in the scene!");
                    enabled = false;
                    return;
                }
            }

            m_Transport = m_NetworkManager.GetComponent<UnityTransport>();
            if (m_Transport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager!");
                enabled = false;
                return;
            }

            // Initialize input fields with default values
            if (m_IpAddressInput != null)
                m_IpAddressInput.text = m_DefaultIp;
            else
                Debug.LogWarning("IP Address Input field not assigned!");
            
            if (m_PortInput != null)
                m_PortInput.text = m_DefaultPort.ToString();
            else
                Debug.LogWarning("Port Input field not assigned!");

            // Set up button listeners
            if (m_StartHostButton != null)
                m_StartHostButton.onClick.AddListener(StartHost);
            else
                Debug.LogWarning("Start Host Button not assigned!");
                
            if (m_StartClientButton != null)
                m_StartClientButton.onClick.AddListener(StartClient);
            else
                Debug.LogWarning("Start Client Button not assigned!");
            
            if (m_DisconnectButton != null)
                m_DisconnectButton.onClick.AddListener(Disconnect);
            else
                Debug.LogWarning("Disconnect Button not assigned!");


            m_NetworkManager.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (m_NetworkManager != null)
            {
                m_NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                m_NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void StartClient()
        {
            UpdateConnectionSettings();
            
            if (m_NetworkManager.StartClient())
            {
                UpdateConnectionStatus("Connecting...");
                if (m_StartClientButton != null) m_StartClientButton.interactable = false;
                if (m_StartHostButton != null) m_StartHostButton.interactable = false;
                StartScreen.SetActive(false);
                roundsOjbect.SetActive(true);
                EventSystem.current.SetSelectedGameObject(AddPlayerButton.gameObject);
            }
            else
            {
                UpdateConnectionStatus("Failed to start client!");
            }
        }

        private void StartHost()
        {
            UpdateConnectionSettings();
            
            if (m_NetworkManager.StartHost())
            {
                UpdateConnectionStatus("Host started. Waiting for connections...");
                StartScreen.SetActive(false);
                roundsOjbect.SetActive(true);
                EventSystem.current.SetSelectedGameObject(AddPlayerButton.gameObject);
            }
            else
            {
                UpdateConnectionStatus("Failed to start host!");
            }
        }

        private void Disconnect()
        {
            if (m_NetworkManager.IsHost || m_NetworkManager.IsServer || m_NetworkManager.IsClient)
            {
                m_NetworkManager.Shutdown();
                UpdateConnectionStatus("Disconnected from server.");
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == m_NetworkManager.LocalClientId)
            {
                UpdateConnectionStatus($"Connected successfully!");
            }
            else if (m_NetworkManager.IsHost || m_NetworkManager.IsServer)
            {
                UpdateConnectionStatus($"Client {clientId} connected!");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == m_NetworkManager.LocalClientId || !m_NetworkManager.IsListening)
            {
                UpdateConnectionStatus("Disconnected from server.");
            }
            else if (m_NetworkManager.IsHost || m_NetworkManager.IsServer)
            {
                UpdateConnectionStatus($"Client {clientId} disconnected!");
            }
        }

        private void UpdateConnectionSettings()
        {
            if (m_IpAddressInput == null || m_PortInput == null || m_Transport == null)
                return;

            string ipAddress = string.IsNullOrEmpty(m_IpAddressInput.text) ? m_DefaultIp : m_IpAddressInput.text;
            
            // Parse port with fallback to default
            if (!ushort.TryParse(m_PortInput.text, out ushort port))
                port = m_DefaultPort;

            m_Transport.ConnectionData.Address = ipAddress;
            m_Transport.ConnectionData.Port = port;
            Debug.Log($"Connection settings updated: IP={ipAddress}, Port={port}");
        }

        private void UpdateConnectionStatus(string message)
        {
            if (m_ConnectionStatusText != null)
            {
                m_ConnectionStatusText.text = message;
                Debug.Log(message);
            }
        }

    }
}
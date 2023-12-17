using System;
using System.Globalization;
using UdonSharp;
using UdonVR.DisBridge;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace Lastation.DisBridge.Playerlist
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class Playerlist : UdonSharpBehaviour
    {
        #region Serialized Fields

        [Tooltip("DisBridge Plugin Manager")]
        [SerializeField] private PluginManager manager;

        [Space]
        [Header("References")]
        [Tooltip("Playerlist Viewport Content Transform")]
        [SerializeField] private Transform PlayerList;

        [Tooltip("Playerlist Text Component References")]
        [SerializeField] private Text textPlayersOnline, textInstanceLifetime, textInstanceMaster, textTimeInWorld;

        [Tooltip("Playerlist Item Prefab/Template")]
        [SerializeField] private GameObject playerListTemplate;

        [Space]
        [Header("Color")]
        [Tooltip("Default Player Color, only applies if player is not in a group.")]
        [SerializeField] private Color32 defaultColor;

        #endregion

        #region Private Variables

        [UdonSynced] private long instanceStartTime = 0;
        private long utcNow;
        private long localJoinTime = 0;
        private VRCPlayerApi localPlayer;
        private VRCPlayerApi[] players;
        private int playerCount;
        private float updateTimer = 0f;
        private const int
            INDEX_ID = 1,
            INDEX_VR = 4,
            INDEX_GROUP1 = 5,
            INDEX_GROUP2 = 6,
            INDEX_TEXT_ID = 0,
            INDEX_TEXT_NAME = 1,
            INDEX_TEXT_TIME = 2;
        private const NumberStyles HEX_NUMSTYLE = NumberStyles.HexNumber;

        private DataList playerDataList;

        private RoleContainer _tmp_RoleContainer;
        private bool hasRole;

        #endregion

        private void Start()
        {
            playerDataList = new DataList();

            manager.AddPlugin(gameObject);

            localPlayer = Networking.LocalPlayer;

            UpdateInstanceMaster();

            UpdateUtcTime();

            localJoinTime = utcNow;

            if (localPlayer.isMaster)
            {
                instanceStartTime = utcNow;

                RequestSerialization();
            }
        }

        private void Update()
        {
            updateTimer += Time.deltaTime;

            if (updateTimer >= 1f)
            {
                UpdateUtcTime();

                textInstanceLifetime.text = GetDuration(instanceStartTime);
                textTimeInWorld.text = GetDuration(localJoinTime);

                updateTimer = 0f;
            }
        }

        private string GetDuration(long ticks)
        {
            return TimeSpan.FromTicks(utcNow - ticks).ToString(@"hh\:mm\:ss");
        }

        private void UpdateUtcTime()
        {
            utcNow = DateTime.UtcNow.ToFileTimeUtc();
        }

        private void UpdateTotalPlayerCount(int count)
        {
            // Get the current list of players
            players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);

            // Update the playerCount based on the incoming count parameter
            playerCount = Math.Max(count, playerCount);

            // Calculate the number of players online (excluding the new player if count < 0)
            int playersOnline = players.Length - (count < 0 ? 1 : 0);

            // Update the UI text with the player count information
            textPlayersOnline.text = $"{playersOnline} / {playerCount}";
        }

        private void AddPlayer(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) { return; }

            if (playerDataList.IndexOf(player.playerId) != -1) return;
            playerDataList.Add(player.playerId);

            if (player.playerId > playerCount) 
            { 
                UpdateTotalPlayerCount(player.playerId); 
            }

            GameObject newPlayerlistPanel = VRCInstantiate(playerListTemplate);

            newPlayerlistPanel.SetActive(true);

            Transform t = newPlayerlistPanel.transform;
            t.SetParent(PlayerList.transform);
            t.localPosition = Vector3.zero;
            t.localEulerAngles = Vector3.zero;
            t.localScale = Vector3.one;

            Text[] texts = t.GetComponentsInChildren<Text>(true);

            texts[INDEX_TEXT_ID].text = player.playerId.ToString();
            texts[INDEX_TEXT_NAME].text = player.displayName;
            texts[INDEX_TEXT_TIME].text = (player.playerId < localPlayer.playerId) ? "Joined before you" : DateTime.UtcNow.ToLocalTime().ToString("dd MMMM yyyy hh:mm:ss");

            if (player.IsUserInVR()) { t.GetChild(INDEX_VR).gameObject.SetActive(true); }

            if (manager != null)
            {
                Debug.Log("[Playerlist] - manager check passed");
                ApplyGroupsInfo(t, player);
            }
        }

        private void ApplyGroupsInfo(Transform playlistPanel, VRCPlayerApi _player)
        {
            hasRole = true;
            _tmp_RoleContainer = manager.GetPlayerRole(_player);
            if (_tmp_RoleContainer == null)
            {
                hasRole = false;
            }

            int shownGroupCount = 0;

            playlistPanel.GetComponent<Image>().color = hasRole ? _tmp_RoleContainer.roleColor : defaultColor;

            if (shownGroupCount < 2)
            {
                GameObject imageGO = playlistPanel.GetChild(INDEX_GROUP1).gameObject;

                Sprite icon = hasRole ? _tmp_RoleContainer.roleIcon : null;

                if (icon != null)
                {
                    imageGO.SetActive(true);

                    imageGO.GetComponent<Image>().sprite = icon;
                }
            }
        }

        private void RemovePlayer(int id)
        {
            for (int i = 0; i < PlayerList.childCount; i++)
            {
                Transform item = PlayerList.GetChild(i);

                if (Convert.ToInt32(item.GetChild(INDEX_ID).GetComponent<Text>().text) == id)
                {
                    Destroy(item.gameObject);

                    return;
                }
            }
        }

        private void UpdateInstanceMaster()
        {
            VRCPlayerApi master = Networking.GetOwner(gameObject);

            if (Utilities.IsValid(master)) { textInstanceMaster.text = master.displayName; }
        }

        public void _UVR_Init()
        {
            for (int i = 0; i < VRCPlayerApi.GetPlayerCount(); i++)
            {
                AddPlayer(VRCPlayerApi.GetPlayerById(i+1));
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (manager.HasInit())
            {
                AddPlayer(player);
            }

        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player)) { RemovePlayer(player.playerId); }

            UpdateInstanceMaster();

            UpdateTotalPlayerCount(-1);
        }
    }
}
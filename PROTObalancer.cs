/* PROTObalancer.cs

by PapaCharlie9@gmail.com

Free to use as is in any way you want with no warranty.

*/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

//Aliases
using EventType = PRoCon.Core.Events.EventType;
using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, None };

public enum BalanceSpeed { Stop, Slow, Adaptive, Fast };

public enum DefineStrong { RoundScore, RoundKDR, RoundKills };

public class PROTObalancer : PRoConPluginAPI, IPRoConPluginInterface
{

/* Inherited:
    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
*/

    public class PerModeSettings {
        public PerModeSettings() {}
        
        public PerModeSettings(String simplifiedModeName) {
            DetermineStrongPlayersBy = DefineStrong.RoundScore;
            
            switch (simplifiedModeName) {
                case "Conquest":
                    MaxPlayers = 64;
                    EstimatedMaxTickets = 300;
                    DefinitionOfHighPopulationPlayers = 48;
                    DefinitionOfLowPopulationPlayers = 16;
                    DefinitionOfEarlyPhaseTickets = 240;
                    DefinitionOfLatePhaseTickets = 60;
                    break;
                case "Rush":
                    MaxPlayers = 32;
                    EstimatedMaxTickets = 75;
                    DefinitionOfHighPopulationPlayers = 24;
                    DefinitionOfLowPopulationPlayers = 8;
                    DefinitionOfEarlyPhaseTickets = 60;
                    DefinitionOfLatePhaseTickets = 15;
                    break;
                case "Scavenger":
                    MaxPlayers = 64;
                    EstimatedMaxTickets = 300;
                    DefinitionOfHighPopulationPlayers = 48;
                    DefinitionOfLowPopulationPlayers = 16;
                    DefinitionOfEarlyPhaseTickets = 240;
                    DefinitionOfLatePhaseTickets = 60;
                    break;
                case "Squad Deathmatch":
                    MaxPlayers = 16;
                    EstimatedMaxTickets = 100;
                    DefinitionOfHighPopulationPlayers = 14;
                    DefinitionOfLowPopulationPlayers = 8;
                    DefinitionOfEarlyPhaseTickets = 80;
                    DefinitionOfLatePhaseTickets = 20;
                    break;
                case "Squad Rush":
                    MaxPlayers = 8;
                    EstimatedMaxTickets = 20;
                    DefinitionOfHighPopulationPlayers = 6;
                    DefinitionOfLowPopulationPlayers = 4;
                    DefinitionOfEarlyPhaseTickets = 18;
                    DefinitionOfLatePhaseTickets = 2;
                    break;
                case "Tank Superiority":
                    MaxPlayers = 64;
                    EstimatedMaxTickets = 200;
                    DefinitionOfHighPopulationPlayers = 48;
                    DefinitionOfLowPopulationPlayers = 16;
                    DefinitionOfEarlyPhaseTickets = 160;
                    DefinitionOfLatePhaseTickets = 40;
                    break;
                case "Team Deathmatch":
                    MaxPlayers = 64;
                    EstimatedMaxTickets = 100;
                    DefinitionOfHighPopulationPlayers = 48;
                    DefinitionOfLowPopulationPlayers = 16;
                    DefinitionOfEarlyPhaseTickets = 80;
                    DefinitionOfLatePhaseTickets = 20;
                    break;
                default:
                    break;
            }
        }
        
        public int MaxPlayers = 64; // will be corrected later
        public int EstimatedMaxTickets = 100; // will be corrected later
        public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
        public double DefinitionOfHighPopulationPlayers = 48;
        public double DefinitionOfLowPopulationPlayers = 16;
        public double DefinitionOfEarlyPhaseTickets = 80;
        public double DefinitionOfLatePhaseTickets = 20;
        
        //public double MinTicketsPercentage = 10.0; // TBD
        public int GoAggressive = 0; // TBD
    }


    public class PlayerStats {
        public PlayerStats() {}
        
        public DateTime FirstSpawnTimestamp;
        public DateTime RoundStartTimestamp;
        public String Tag;
        public int Score;
        public int Kills;
        public int Deaths;
        public int Rounds; // incremented OnRoundOverPlayers
        
        public int ScoreTotal; // not including current round
        public int KillsTotal; // not including current round
        public int DeathsTotal; // not including current round
    }


private bool fIsEnabled;
private Dictionary<String,String> fModeToSimple = null;
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

/* Settings */

private int DebugLevel;
private bool QuietMode;
private int MaxSlots;
private int MaxSwitchesStrong;
private int MaxSwitchesWeak;
private double FirstMinutesAnySwitchingAllowed;
private bool Enable2SlotReserve;
private bool EnablerecruitCommand;
private bool EnableExclusionsWhenAssigningNewPlayersToTeams;
private bool EnableWhitelistingOfReservedSlotsList;
private String[] Whitelist;
private String[] Blacklist;
private PresetItems Preset;

private bool OnWhitelist;
private bool TopScorers;
private bool SameClanTagsInSquad;
private bool ClanTagFetchPending;
private double MinutesSinceFirstSpawn;

private double LowPopulationTicketPercentageToUnstack;
private double MediumPopulationTicketPercentageToUnstack;
private double HighPopulationTicketPercentageToUnstack;
private BalanceSpeed LowPopulationBalanceSpeed;
private BalanceSpeed MediumPopulationBalanceSpeed;
private BalanceSpeed HighPopulationBalanceSpeed;

private double EarlyPhaseTicketPercentageToUnstack;
private double MidPhaseTicketPercentageToUnstack;
private double LatePhaseTicketPercentageToUnstack;
private BalanceSpeed EarlyPhaseBalanceSpeed;
private BalanceSpeed MidPhaseBalanceSpeed;
private BalanceSpeed LatePhaseBalanceSpeed;

private String ChatMovedForBalance;
private String YellMovedForBalance;
private String ChatMovedToUnstack;
private String YellMovedToUnstack;
private String ChatDetectedSwitchToWinningTeam;
private String YellDetectedSwitchToWinningTeam;
private String ChatDetectedSwitchToLosingTeam;
private String YellDetectedSwitchToLosingTeam;
private String ChatAfterUnswitching;
private String YellAfterUnswitching;

private String ShowInLog; // command line to show info in plugin.log
private bool LogChat;

/* Constructor */

public PROTObalancer() {
    /* Private members */
    fIsEnabled = false;
    fModeToSimple = new Dictionary<String,String>();

    fEasyTypeDict = new Dictionary<int, Type>();
    fEasyTypeDict.Add(0, typeof(int));
    fEasyTypeDict.Add(1, typeof(Int16));
    fEasyTypeDict.Add(2, typeof(Int32));
    fEasyTypeDict.Add(3, typeof(Int64));
    fEasyTypeDict.Add(4, typeof(float));
    fEasyTypeDict.Add(5, typeof(long));
    fEasyTypeDict.Add(6, typeof(String));
    fEasyTypeDict.Add(7, typeof(string));
    fEasyTypeDict.Add(8, typeof(double));

    fBoolDict = new Dictionary<int, Type>();
    fBoolDict.Add(0, typeof(Boolean));
    fBoolDict.Add(1, typeof(bool));

    fListStrDict = new Dictionary<int, Type>();
    fListStrDict.Add(0, typeof(List<String>));
    fListStrDict.Add(1, typeof(List<string>));
    
    fPerMode = new Dictionary<String,PerModeSettings>();
    
    /* Settings */

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = 2;
    QuietMode = false; // false: chat is global, true: chat is private. Yells are always private
    MaxSlots = 64;
    MaxSwitchesStrong = 1;
    MaxSwitchesWeak = 2;
    FirstMinutesAnySwitchingAllowed = 5.0;
    Enable2SlotReserve = false;
    EnablerecruitCommand = false;
    EnableExclusionsWhenAssigningNewPlayersToTeams = false;
    EnableWhitelistingOfReservedSlotsList = true;
    Whitelist = new String[] {"[-- name, tag, or EA_GUID --]"};
    Blacklist = new String[] {"[-- name, tag, or EA_GUID --]"};
    Preset = PresetItems.Standard;
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = true;
    TopScorers = true;
    SameClanTagsInSquad = true;
    ClanTagFetchPending = true;
    MinutesSinceFirstSpawn = 5.0;

    /* ===== SECTION 3 - Server Population ===== */

    LowPopulationTicketPercentageToUnstack = 120.0;
    MediumPopulationTicketPercentageToUnstack = 120.0;
    HighPopulationTicketPercentageToUnstack = 120.0;

    LowPopulationBalanceSpeed = BalanceSpeed.Adaptive;
    MediumPopulationBalanceSpeed = BalanceSpeed.Adaptive;
    HighPopulationBalanceSpeed = BalanceSpeed.Slow;

    /* ===== SECTION 4 - Round Phase Settings ===== */

    EarlyPhaseTicketPercentageToUnstack = 120.0;
    MidPhaseTicketPercentageToUnstack = 120.0;
    LatePhaseTicketPercentageToUnstack = 0.0;

    EarlyPhaseBalanceSpeed = BalanceSpeed.Adaptive;
    MidPhaseBalanceSpeed = BalanceSpeed.Adaptive;
    LatePhaseBalanceSpeed = BalanceSpeed.Slow;

    /* ===== SECTION 5 - Messages ===== */
    
    ChatMovedForBalance = "*** MOVED %name% for balance ...";
    YellMovedForBalance = "Moved %name% for balance ...";
    ChatMovedToUnstack = "*** MOVED %name% to unstack teams ...";
    YellMovedToUnstack = "Moved %name% to unstack teams ...";
    ChatDetectedSwitchToWinningTeam = "%name%, the %fromTeam% needs your help, sending you back ...";
    YellDetectedSwitchToWinningTeam = "The %fromTeam% needs your help, sending you back!";
    ChatDetectedSwitchToLosingTeam = "%name%, thanks for helping out the %toTeam%!";
    YellDetectedSwitchToLosingTeam = "Thanks for helping out the %toTeam%!";
    ChatAfterUnswitching = "%name%, please stay on the %toTeam% for the rest of this round";
    YellAfterUnswitching = "Please stay on the %toTeam% for the rest of this round";
    
    /* ===== SECTION 6 - TBD ===== */

    /* ===== SECTION 7 - TBD ===== */

    /* ===== SECTION 8 - Per-Mode Settings ===== */

    /* ===== SECTION 9 - Debug Settings ===== */

    ShowInLog = String.Empty;
    LogChat = true;
}

/* Types */

public enum MessageType { Warning, Error, Exception, Normal };

/* Properties */

public String FormatMessage(String msg, MessageType type) {
    String prefix = "[^b" + GetPluginName() + "^n] ";

    if (type.Equals(MessageType.Warning))
        prefix += "^1^bWARNING^0^n: ";
    else if (type.Equals(MessageType.Error))
        prefix += "^1^bERROR^0^n: ";
    else if (type.Equals(MessageType.Exception))
        prefix += "^1^bEXCEPTION^0^n: ";

    return prefix + msg;
}


public void LogWrite(String msg)
{
    this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
}

public void ConsoleWrite(String msg, MessageType type)
{
    LogWrite(FormatMessage(msg, type));
}

public void ConsoleWrite(String msg)
{
    ConsoleWrite(msg, MessageType.Normal);
}

public void ConsoleWarn(String msg)
{
    ConsoleWrite(msg, MessageType.Warning);
}

public void ConsoleError(String msg)
{
    ConsoleWrite(msg, MessageType.Error);
}

public void ConsoleException(String msg)
{
    ConsoleWrite(msg, MessageType.Exception);
}

public void DebugWrite(String msg, int level)
{
    if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
}


public void ServerCommand(params String[] args)
{
    List<String> list = new List<String>();
    list.Add("procon.protected.send");
    list.AddRange(args);
    this.ExecuteCommand(list.ToArray());
}


public String GetPluginName() {
    return "PROTObalancer";
}

public String GetPluginVersion() {
    return "0.0.0.2";
}

public String GetPluginAuthor() {
    return "PapaCharlie9";
}

public String GetPluginWebsite() {
    return "TBD";
}

public String GetPluginDescription() {
    return HTML_DOC;
}




public List<CPluginVariable> GetDisplayPluginVariables() {


    List<CPluginVariable> lstReturn = new List<CPluginVariable>();

    try {
        /* ===== SECTION 1 - Settings ===== */
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));

        lstReturn.Add(new CPluginVariable("1 - Settings|Quiet Mode", QuietMode.GetType(), QuietMode));

        lstReturn.Add(new CPluginVariable("1 - Settings|Max Slots", MaxSlots.GetType(), MaxSlots));

        lstReturn.Add(new CPluginVariable("1 - Settings|Max Switches: Strong", MaxSwitchesStrong.GetType(), MaxSwitchesStrong));

        lstReturn.Add(new CPluginVariable("1 - Settings|Max Switches: Weak", MaxSwitchesWeak.GetType(), MaxSwitchesWeak));

        lstReturn.Add(new CPluginVariable("1 - Settings|First Minutes Any Switching Allowed", FirstMinutesAnySwitchingAllowed.GetType(), FirstMinutesAnySwitchingAllowed));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable 2 Slot Reserve", Enable2SlotReserve.GetType(), Enable2SlotReserve));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable @#!recruit Command", EnablerecruitCommand.GetType(), EnablerecruitCommand));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Exclusions When Assigning New Players To Teams", EnableExclusionsWhenAssigningNewPlayersToTeams.GetType(), EnableExclusionsWhenAssigningNewPlayersToTeams));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Whitelisting Of Reserved Slots List", EnableWhitelistingOfReservedSlotsList.GetType(), EnableWhitelistingOfReservedSlotsList));

        lstReturn.Add(new CPluginVariable("1 - Settings|Whitelist", Whitelist.GetType(), Whitelist));

        lstReturn.Add(new CPluginVariable("1 - Settings|Blacklist", Blacklist.GetType(), Blacklist));

        String var_name = "1 - Settings|Preset";
        String var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), Preset)));

        /* ===== SECTION 2 - Exclusions ===== */

        lstReturn.Add(new CPluginVariable("2 - Exclusions|On Whitelist", OnWhitelist.GetType(), OnWhitelist));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Top Scorers", TopScorers.GetType(), TopScorers));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Squad", SameClanTagsInSquad.GetType(), SameClanTagsInSquad));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Clan Tag Fetch Pending", ClanTagFetchPending.GetType(), ClanTagFetchPending));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes Since First Spawn", MinutesSinceFirstSpawn.GetType(), MinutesSinceFirstSpawn));

        /* ===== SECTION 3 - Server Population Setttings ===== */

        lstReturn.Add(new CPluginVariable("3 - Population Settings|Low Population: Ticket Percentage To Unstack", LowPopulationTicketPercentageToUnstack.GetType(), LowPopulationTicketPercentageToUnstack));

        lstReturn.Add(new CPluginVariable("3 - Population Settings|Medium Population: Ticket Percentage To Unstack", MediumPopulationTicketPercentageToUnstack.GetType(), MediumPopulationTicketPercentageToUnstack));

        lstReturn.Add(new CPluginVariable("3 - Population Settings|High Population: Ticket Percentage To Unstack", HighPopulationTicketPercentageToUnstack.GetType(), HighPopulationTicketPercentageToUnstack));

        var_name = "3 - Population Settings|Low Population: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), LowPopulationBalanceSpeed)));

        var_name = "3 - Population Settings|Medium Population: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), MediumPopulationBalanceSpeed)));

        var_name = "3 - Population Settings|High Population: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), HighPopulationBalanceSpeed)));

        /* ===== SECTION 4 - Round Phase Setttings ===== */

        lstReturn.Add(new CPluginVariable("4 - Round Phase Settings|Early Phase: Ticket Percentage To Unstack", EarlyPhaseTicketPercentageToUnstack.GetType(), EarlyPhaseTicketPercentageToUnstack));

        lstReturn.Add(new CPluginVariable("4 - Round Phase Settings|Mid Phase: Ticket Percentage To Unstack", MidPhaseTicketPercentageToUnstack.GetType(), MidPhaseTicketPercentageToUnstack));

        lstReturn.Add(new CPluginVariable("4 - Round Phase Settings|Late Phase: Ticket Percentage To Unstack", LatePhaseTicketPercentageToUnstack.GetType(), LatePhaseTicketPercentageToUnstack));

        var_name = "4 - Round Phase Settings|Early Phase: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), EarlyPhaseBalanceSpeed)));

        var_name = "4 - Round Phase Settings|Mid Phase: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), MidPhaseBalanceSpeed)));

        var_name = "4 - Round Phase Settings|Late Phase: Balance Speed";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BalanceSpeed))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BalanceSpeed), LatePhaseBalanceSpeed)));

        /* ===== SECTION 5 - Messages ===== */
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved For Balance", ChatMovedForBalance.GetType(), ChatMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved For Balance", YellMovedForBalance.GetType(), YellMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved To Unstack", ChatMovedToUnstack.GetType(), ChatMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved To Unstack", YellMovedToUnstack.GetType(), YellMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Switch To Winning Team", ChatDetectedSwitchToWinningTeam.GetType(), ChatDetectedSwitchToWinningTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Switch To Winning Team", YellDetectedSwitchToWinningTeam.GetType(), YellDetectedSwitchToWinningTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Switch To Losing Team", ChatDetectedSwitchToLosingTeam.GetType(), ChatDetectedSwitchToLosingTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Switch To Losing Team", YellDetectedSwitchToLosingTeam.GetType(), YellDetectedSwitchToLosingTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: After Unswitching", ChatAfterUnswitching.GetType(), ChatAfterUnswitching));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: After Unswitching", YellAfterUnswitching.GetType(), YellAfterUnswitching));

        /* ===== SECTION 6 - TBD ===== */

        /* ===== SECTION 7 - TBD ===== */

        /* ===== SECTION 8 - Per-Mode Settings ===== */

        List<String> simpleModes = GetSimplifiedModes();

        foreach (String sm in simpleModes) {
            PerModeSettings oneSet = null;
            if (!fPerMode.ContainsKey(sm)) {
                oneSet = new PerModeSettings(sm);
                fPerMode[sm] = oneSet;
            } else {
                oneSet = fPerMode[sm];
            }

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Estimated Max Tickets", oneSet.EstimatedMaxTickets.GetType(), oneSet.EstimatedMaxTickets));

            var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
            var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

            lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population: Players >=", oneSet.DefinitionOfHighPopulationPlayers.GetType(), oneSet.DefinitionOfHighPopulationPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population: Players <=", oneSet.DefinitionOfLowPopulationPlayers.GetType(), oneSet.DefinitionOfLowPopulationPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase: Tickets >=", oneSet.DefinitionOfEarlyPhaseTickets.GetType(), oneSet.DefinitionOfEarlyPhaseTickets));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase: Tickets <=", oneSet.DefinitionOfLatePhaseTickets.GetType(), oneSet.DefinitionOfLatePhaseTickets));

            /*
            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Min Tickets Percentage", oneSet.MinTicketsPercentage.GetType(), oneSet.MinTicketsPercentage));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Go Aggressive", oneSet.GoAggressive.GetType(), oneSet.GoAggressive));
            */
        }

        /* ===== SECTION 9 - Debug Settings ===== */

        lstReturn.Add(new CPluginVariable("9 - Debugging|Show In Log", ShowInLog.GetType(), ShowInLog));

        lstReturn.Add(new CPluginVariable("9 - Debugging|Log Chat", LogChat.GetType(), LogChat));


    } catch (Exception e) {
        ConsoleException(e.ToString());
    }

    return lstReturn;
}

public List<CPluginVariable> GetPluginVariables() {
    return GetDisplayPluginVariables();
}

public void SetPluginVariable(String strVariable, String strValue) {

    DebugWrite(strVariable + " <- " + strValue, 3);

    try {
        String tmp = strVariable;
        int pipeIndex = strVariable.IndexOf('|');
        if (pipeIndex >= 0) {
            pipeIndex++;
            tmp = strVariable.Substring(pipeIndex, strVariable.Length - pipeIndex);
        }

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        String propertyName = Regex.Replace(tmp, @"[^a-zA-Z_0-9]", String.Empty);

        FieldInfo field = this.GetType().GetField(propertyName, flags);
        
        Type fieldType = null;


        if (!tmp.Contains("Settings for") && field != null) {
            fieldType = field.GetValue(this).GetType();
            if (strVariable.Contains("Preset")) {
                fieldType = typeof(PresetItems);
                try {
                    Preset = (PresetItems)Enum.Parse(fieldType, strValue);
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (strVariable.Contains("Balance Speed")) {
                fieldType = typeof(BalanceSpeed);
                try {
                    field.SetValue(this, (BalanceSpeed)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (fEasyTypeDict.ContainsValue(fieldType)) {
                field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
            } else if (fListStrDict.ContainsValue(fieldType)) {
                field.SetValue(this, new List<string>(CPluginVariable.DecodeStringArray(strValue)));
            } else if (fBoolDict.ContainsValue(fieldType)) {
                DebugWrite(propertyName + " strValue = " + strValue, 3);
                if (Regex.Match(strValue, "True", RegexOptions.IgnoreCase).Success) {
                    field.SetValue(this, true);
                } else {
                    field.SetValue(this, false);
                }
            }
        } else {
            Match m = Regex.Match(tmp, @"([^:]+):\s([^:]+)$");
            
            if (m.Success) {
                String mode = m.Groups[1].Value;
                String perModeName = m.Groups[2].Value.Replace(" ","");
                
                if (!fPerMode.ContainsKey(mode)) {
                    fPerMode[mode] = new PerModeSettings(mode);
                }
                PerModeSettings pms = fPerMode[mode];
                
                field = pms.GetType().GetField(perModeName, flags);
                
                if (field != null) {
                    fieldType = field.GetValue(pms).GetType();
                    if (fEasyTypeDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                    } else if (fListStrDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, new List<string>(CPluginVariable.DecodeStringArray(strValue)));
                    } else if (fBoolDict.ContainsValue(fieldType)) {
                        if (Regex.Match(strValue, "True", RegexOptions.IgnoreCase).Success) {
                            field.SetValue(pms, true);
                        } else {
                            field.SetValue(pms, false);
                        }
                    } else if (strVariable.Contains("Strong")) {
                        fieldType = typeof(DefineStrong);
                        try {
                            field.SetValue(pms, (DefineStrong)Enum.Parse(fieldType, strValue));
                        } catch (Exception e) {
                            ConsoleException(e.ToString());
                        }
                    }
                } else {
                    DebugWrite("field is null", 3);
                }
            }
        }
    } catch (System.Exception e) {
        ConsoleException(e.ToString());
    } finally {
        // TBD: Validate() function needed here!
        // Validate all values and correct if needed
        
        if (!String.IsNullOrEmpty(ShowInLog)) {
            if (Regex.Match(ShowInLog, @"modes", RegexOptions.IgnoreCase).Success) {
                List<String> modeList = GetSimplifiedModes();
                DebugWrite("modes(" + modeList.Count + "):", 2);
                foreach (String m in modeList) {
                    DebugWrite(m, 2);
                }
            }
            
            ShowInLog = String.Empty;
        }

                
        /*
        switch (perModeName) {
            case "Min Tickets":
                if (!Double.TryParse(strValue, out pms.MinTicketsPercentage)) {
                    ConsoleError("Bogus setting for " + strVariable + " ? " + strValue);
                }
                break;
            case "Go Aggressive":
                if (!Int32.TryParse(strValue, out pms.GoAggressive)) {
                    ConsoleError("Bogus setting for " + strVariable + " ? " + strValue);
                }
                break;
        }
        */
        
        // Update Preset value based on current settings
        // UpdatePresetValue(); // TBD
    }
}


public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
    this.RegisterEvents(this.GetType().Name, 
    "OnVersion",
    "OnServerInfo",
    "OnResponseError",
    "OnListPlayers",
    "OnPlayerJoin",
    "OnPlayerLeft",
    "OnPlayerKilled",
    "OnPlayerSpawned",
    "OnPlayerTeamChange",
    "OnGlobalChat",
    "OnTeamChat",
    "OnSquadChat",
    "OnRoundOverPlayers",
    "OnRoundOver",
    "OnRoundOverTeamScores",
    "OnLoadingLevel",
    "OnLevelStarted",
    "OnLevelLoaded"
    );
}

public void OnPluginEnable() {
    fIsEnabled = true;
    ConsoleWrite("Enabled! Version = " + GetPluginVersion());
}


private void JoinWith(Thread thread, int secs)
{
    if (thread == null || !thread.IsAlive)
        return;

    ConsoleWrite("Waiting for ^b" + thread.Name + "^n to finish");
    thread.Join(secs*1000);
}


public void OnPluginDisable() {
    fIsEnabled = false;
    
    ConsoleWrite("Disabled!");
}


public override void OnVersion(String type, String ver) {
    DebugWrite("OnVersion " + type + " " + ver, 9);
}

private CServerInfo fSI = null;

public override void OnServerInfo(CServerInfo serverInfo) {
    DebugWrite("Debug level = " + DebugLevel, 9);
    
    fSI = serverInfo;
}

public override void OnResponseError(List<String> requestWords, String error) { }

public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
}

public override void OnPlayerJoin(String soldierName) {
}

public override void OnPlayerLeft(CPlayerInfo playerInfo) {
}

public override void OnPlayerKilled(Kill kKillerVictimDetails) { }

public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) { }

public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) { }

public override void OnGlobalChat(String speaker, String message) { }

public override void OnTeamChat(String speaker, String message, int teamId) { }

public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

public override void OnRoundOverPlayers(List<CPlayerInfo> players) { }

public override void OnRoundOverTeamScores(List<TeamScore> teamScores) { }

public override void OnRoundOver(int winningTeamId) { }

public override void OnLoadingLevel(String mapFileName, int roundsPlayed, int roundsTotal) { }

public override void OnLevelStarted() { }

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) { } // BF3


/* ===== */

public List<String> GetSimplifiedModes() {
    List<String> r = new List<String>();
    
    if (fModeToSimple.Count < 1) {
        List<CMap> raw = this.GetMapDefines();
        foreach (CMap m in raw) {
            String simple = null;
            if (Regex.Match(m.GameMode, @"(?:Conquest|Assault)").Success) {
                simple = "Conquest";
            } else if (Regex.Match(m.GameMode, @"TDM").Success) {
                simple = "Team Deathmatch";
            } else if (Regex.Match(m.GameMode, @"Gun Master").Success) {
                continue; // not supported
            } else {
                simple = m.GameMode;
            }
            if (fModeToSimple.ContainsKey(m.PlayList)) {
                if (fModeToSimple[m.PlayList] != simple) {
                    ConsoleWarn("For mode " + m.PlayList + " old value " + fModeToSimple[m.PlayList] + " != new value " + simple);
                }
            } else {
                fModeToSimple[m.PlayList] = simple;
            }
        }
    }
    
    bool last = false;
    foreach (KeyValuePair<String,String> p in fModeToSimple) {
        if (r.Contains(p.Value)) continue;
        if (p.Value == "Squad Rush") { last = true; continue; }
        r.Add(p.Value); // collect up all the simple GameMode names
    }
    if (last) r.Add("Squad Rush"); // make sure this is last

    return r;
}







/* ========================================================================== */











private const String HTML_DOC = @"
<h1>Multi-Balancer &amp; Unstacker, including SQDM</h1>
<p>For BF3, this plugin does live round team balancing and unstacking for all game modes, including Squad Deathmatch (SQDM).</p>

<h2>THIS IS JUST A PROTOTYPE FOR FEEDBACK!</h2>
<p>It doesn't do any balancing or unstacking, no matter what you change the settings to. It is completely safe to run on an active server. It sends no commands to the server.</p>

<p><font color=#0000FF>The purpose of this prototype is to get feedback from users about the arrangement and usage of plugin settings. I'm interested in answers to these questions:</font></p>

<ul>
<li>What is your overall impression with the number and complexity of settings?</li>
<li>What's missing?</li>
<li>What should be added?</li>
<li>Which names or values are confusing and need additional documentation to clarify?</li>
<li>What should be changed to improve clarity?</li>
</ul>

<h2>Description</h2>
<p>TBD</p>

<h2>Commands</h2>
<p>TBD</p>

<h2>Settings</h2>
<p>TBD</p>

<h2>Development</h2>
<p>TBD</p>
<h3>Changelog</h3>
<blockquote><h4>0.0.0.1 (10-JAN-2013)</h4>
    - initial version<br/>
</blockquote>
";


} // end PROTObalancer

} // end namespace PRoConEvents




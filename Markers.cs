using System;
using System.IO;
using System.Runtime.InteropServices;

public class CPHInline
{
    // Adds to the marker file on the filepath.
	public void AddMarkerToEDL(string filePath, string timecode, string color, string name, string description)
    {
        int lineCounter = 0;
        // Check if file exists and read its contents
        if (File.Exists(filePath))
        {
			//update the lineCounter with how many lines the file has.
            lineCounter = File.ReadAllLines(filePath).Length/2;
			// Prepare the new entry with incremented counter
			string newEntry = $"{lineCounter:D3} 001 V {timecode} {timecode} {timecode} {timecode}\n{description}|C:{color} |M:{name} |D:1";

			// Append the new entry to the file
			File.AppendAllText(filePath, newEntry + Environment.NewLine);
        }
    }
    // Turns OBS timestamps into Resolve EDL compatible timestamps. Omits frames/ms field due to framerate desyncs, the accuracy isn't needed anyways.
    	public string? GetResolveTimecode(string timecode) {
		if (!TimeSpan.TryParseExact(timecode, "hh\\:mm\\:ss\\.fff", null, out TimeSpan EventTime))
		{
			CPH.LogError("Invalid timecode " + timecode);
			return null;
		}
		string TimeCode =	string.Format(  "{0:D2}:{1:D2}:{2:D2}:00",
                                            (int)EventTime.TotalHours,
                                            EventTime.Minutes,
                                            EventTime.Seconds);
		return TimeCode;
	}

	public bool Execute()
	{
        if (!CPH.TryGetArg("obs.outputTimecode", out string TimeCode))
        {
            CPH.ShowToastNotification("Berry error", "Marker could not be added, missing timecode... \n Error logged.");
            CPH.LogError("Missing OBS timecode.");
            return false;
        }
        if (!CPH.TryGetArg("obs.outputActive", out string OutputActive))
        {
            CPH.ShowToastNotification("Berry error", "Marker could not be added, you are not recording?... How??? \n Error logged.");
            CPH.LogError("Not recording, somehow.");
            return false;
        }
        string LastRecPath = CPH.GetGlobalVar<string>("LastRecPath");
        if (!CPH.TryGetArg("obs.recordDirectory", out string RecordDir))
        {
            CPH.ShowToastNotification("Berry error", "Default default directory not found... is OBS connected?");
            CPH.LogError("Recording directory field missing.");
            return false;
        }
        if (LastRecPath == null)
        {
            CPH.ShowToastNotification("Berry error", "Recording path missing... falling back to generic recording folder entry. \n Error logged.");
            CPH.LogError("Record path missing");
            LastRecPath = RecordDir + "/fallback" + DateTime.Now.ToString("HHmmss") + ".mp4";
            CPH.SetGlobalVar("LastRecPath",LastRecPath);
        }
        string EDLPath = Path.ChangeExtension(LastRecPath, ".edl"); // gets a path for the EDL file by changing the extension to EDL
        string FileHeader = @"TITLE: Timeline 1
FCM: NON-DROP FRAME"; // file header
        if (!File.Exists(EDLPath)) // if file doesn't exist, create it and add the header
        {
            using (StreamWriter sw = File.CreateText(EDLPath))
            {
                sw.WriteLine(FileHeader);
            }
        }
        if (CPH.TryGetArg("__source", out string source)) // Trigger exists, therefore it's either a command, test trigger or manually triggered from the chat.
        {
            if (source == "CommandTriggered") // when ran from a command.
            {
                string color = "ResolveColorPurple"; // purple color indicates a command.
                CPH.TryGetArg("user", out string User);
                CPH.TryGetArg("broadcastUser", out string BroadcastUser);

                if (User == BroadcastUser)
                {
                    color = "ResolveColorRed"; // Set the marker as red if the command is from the broadcaster, for brevity.
                }
                CPH.TryGetArg("rawInput", out string message);
                message ??= "";
                User ??= "Berry[Fallback]";
                string cleanMessage = message.Replace("\n", "").Replace("\r", "").Replace("\t", "");
                AddMarkerToEDL(EDLPath, GetResolveTimecode(TimeCode), color, User, cleanMessage);
                return true;
            }
            else if (source == "ObsRecordingStarted")
            {
                string color = "ResolveColorCyan"; // cyan color indicates OBS recording start.
                string name = "Berry[recording start]";
                AddMarkerToEDL(EDLPath, GetResolveTimecode(TimeCode), color, name, " ");
                return true;
            }
        }
        else // lacking a source usually means it was run from the streamer.bot deck, either by a moderator or the broadcaster.
        {
            if (!CPH.TryGetArg("streamerbotUserUsername", out string deckUsername))
            {
                CPH.ShowToastNotification("Berry error", "No deck username found, panicked and quit.");
                CPH.LogError("Streamerbot deck username missing, big bad");
                return false;
            }
            string name = deckUsername.Substring(0, deckUsername.Length - 2);
            CPH.TryGetArg("markerDescription", out string description);
            description ??= "";
            string cleanDescription = description.Replace("\n", "").Replace("\r", "").Replace("\t", "");
            string color = "ResolveColorGreen";
            AddMarkerToEDL(EDLPath, GetResolveTimecode(TimeCode), color, name, cleanDescription);
            return true;
        }
        return false;
    }
}
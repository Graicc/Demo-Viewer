﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class Game
{
	public bool isNewstyle;
	public int nframes;
	public string filename;
	public List<string> rawFrames;
	public List<Frame> frames { private get; set; }
	public Mesh pointCloud;

	/// <summary>
	/// Gets or converts the requested frame.
	/// May return null if the frame can't be converted.
	/// </summary>
	public Frame GetFrame(int index)
	{
		if (frames[index] != null) return frames[index];
		
		// repeat because maybe the requested frame needs to be discarded.
		while (rawFrames.Count > 0)
		{
			Frame newFrame = Frame.FromEchoReplayString(rawFrames[index]);
			if (newFrame != null)
			{
				frames[index] = newFrame;
				// rawFrames[index] = null;    // free up the memory, since the raw frames take up a lot more
				return frames[index];
			}

			Debug.LogError($"Discarded frame {index}");
			frames.RemoveAt(index);
			rawFrames.RemoveAt(index);
			nframes--;
		}
		Debug.LogError("File contains no valid arena frames.");
		return null;
	}
}


[Serializable]
public class Stats
{
	public float possession_time;
	public int points;
	public int goals;
	public int saves;
	public int stuns;
	public int interceptions;
	public int blocks;
	public int passes;
	public int catches;
	public int steals;
	public int assists;
	public int shots_taken;

	public override string ToString()
	{
		StringBuilder s = new StringBuilder();
		s.Append("Possession Time: ");
		s.Append(possession_time.ToString("N0"));
		s.Append("\nPoints: ");
		s.Append(points);
		s.Append("\nGoals: ");
		s.Append(goals);
		s.Append("\nSaves: ");
		s.Append(saves);
		s.Append("\nStuns: ");
		s.Append(stuns);
		s.Append("\nAssists: ");
		s.Append(assists);
		s.Append("\nShots Taken: ");
		s.Append(shots_taken);
		return s.ToString();
	}
}


[Serializable]
public class Last_Score
{
	public float disc_speed;
	public string team;
	public string goal_type;
	public int point_amount;
	public float distance_thrown;
	public string person_scored;
	public string assist_scored;
}


public enum TeamColor : byte { blue, orange, spectator }

[Serializable]
public class Frame
{

	/// <summary>
	/// Time of this frame as saved in the replay file
	/// </summary>
	public DateTime frameTime;

	/// <summary>
	/// The original JSON string for network transfer
	/// </summary>
	public string originalJSON;

	public Disc disc;
	public string sessionid;
	public bool orange_team_restart_request;
	public string sessionip = string.Empty;
	/// <summary>
	/// The current state of the match
	/// { pre_match, round_start, playing, score, round_over, pre_sudden_death, sudden_death, post_sudden_death, post_match }
	/// </summary>
	public string game_status;
	/// <summary>
	/// Game time as displayed in game.
	/// </summary>
	public string game_clock_display;
	/// <summary>
	/// Time of remaining in match (in seconds)
	/// </summary>
	public float game_clock;
	public string match_type;
	public string map_name { get; set; }
	/// <summary>
	/// Name of the oculus username recording.
	/// </summary>
	public string client_name;
	public Playspace player;
	public int orange_points;
	public bool private_match;
	/// <summary>
	/// List of integers to determine who currently has possession.
	/// [ team, player ]
	/// </summary>
	public int[] possession;
	public bool tournament_match;
	public bool blue_team_restart_request;
	public int blue_points;
	/// <summary>
	/// Object containing data from the last goal made.
	/// </summary>
	public Last_Score last_score;
	public Team[] teams;

	public static Frame FromEchoReplayString(string line)
	{
		if (!string.IsNullOrEmpty(line))
		{
			string[] splitJSON = line.Split('\t');
			string onlyJSON, onlyTime;
			if (splitJSON.Length == 2)
			{
				onlyJSON = splitJSON[1];
				onlyTime = splitJSON[0];
			}
			else
			{
				Debug.LogError("Row doesn't include both a time and API JSON");
				return null;
			}

			if (onlyTime.Length == 23 && onlyTime[13] == '.')
			{
				StringBuilder sb = new StringBuilder(onlyTime);
				sb[13] = ':';
				sb[16] = ':';
				onlyTime = sb.ToString();
			}
			
			if (!DateTime.TryParse(onlyTime, out DateTime frameTime))
			{
				Debug.LogError($"Can't parse date: {onlyTime}");
				return null;
			}

			// if this is actually valid arena data
			if (onlyJSON.Length > 800)
			{
				return FromJSON(frameTime, onlyJSON);
			}
			else
			{
				Debug.LogError("Row is not arena data.");
				return null;
			}
		}
		else
		{
			Debug.LogError("String is empty");
			return null;
		}
	}

	/// <summary>
	/// Creates a frame from json and a timestamp
	/// </summary>
	/// <param name="time">The time the frame was recorded</param>
	/// <param name="json">The json for the frame</param>
	/// <returns>A Frame object</returns>
	public static Frame FromJSON(DateTime time, string json)
	{
		if (string.IsNullOrEmpty(json)) return null;

		try
		{
			Frame frame = JsonConvert.DeserializeObject<Frame>(json);
			frame.frameTime = time;
			frame.originalJSON = json;
			return frame;
		} catch (JsonReaderException ex)
		{
			return null;
		}
	}

	/// <summary>
	/// ↔ Mixes the two frames with a linear interpolation based on t
	/// For binary or int values, the "from" frame is preferred.
	/// </summary>
	/// <param name="from">The start frame</param>
	/// <param name="to">The next frame</param>
	/// <param name="t">The DateTime of the playhead</param>
	/// <returns>A mix of the two frames</returns>
	internal static Frame Lerp(Frame from, Frame to, DateTime t)
	{
		// of the frames is null, return the other, even if it is null
		if (from == null) return to;
		if (to == null) return from;
		
		// the frames are the same
		if (from.frameTime == to.frameTime) return from;
		
		// t is out of bounds
		if (from.frameTime >= t) return from;
		if (to.frameTime <= t) return to;

		// the ratio between the frames
		float lerpValue = (float)((t - from.frameTime).TotalSeconds / (to.frameTime - from.frameTime).TotalSeconds);

		Frame newFrame = new Frame()
		{
			frameTime = t,


			disc = Disc.Lerp(from.disc, to.disc, lerpValue),
			sessionid = from.sessionid,
			orange_team_restart_request = from.orange_team_restart_request,
			sessionip = from.sessionip,
			game_status = from.game_status,
			game_clock_display = from.game_clock_display, // TODO this could be interpolated
			game_clock = Mathf.Lerp(from.game_clock, to.game_clock, lerpValue),
			match_type = from.match_type,
			map_name = from.map_name,
			client_name = from.client_name,
			player = Playspace.Lerp(from.player, to.player, lerpValue),
			orange_points = from.orange_points,
			private_match = from.private_match,
			possession = from.possession,
			tournament_match = from.tournament_match,
			blue_team_restart_request = from.blue_team_restart_request,
			blue_points = from.blue_points,
			last_score = from.last_score

		};

		int numTeams = Math.Max(from.teams.Length, to.teams.Length);

		newFrame.teams = new Team[numTeams];

		for (int i = 0; i < numTeams; i++)
		{
			if (from.teams.Length <= i &&
			    to.teams.Length > i)
			{
				newFrame.teams[i] = to.teams[i];
			}
			else if (to.teams.Length <= i &&
			         from.teams.Length > i)
			{
				newFrame.teams[i] = from.teams[i];
			}
			else if (from.teams.Length > i &&
			         to.teams.Length > i)
			{
				// actually lerp the team
				newFrame.teams[i] = Team.Lerp(from.teams[i], to.teams[i], lerpValue);
			}
		}

		return newFrame;
	}
}

[Serializable]
public class Disc
{
	public float[] position;
	public float[] forward;
	public float[] left;
	public float[] up;
	public float[] velocity;
	public int bounce_count;

	/// <summary>
	/// ↔ Mixes the two states with a linear interpolation based on t
	/// For binary or int values, the "from" state is preferred.
	/// </summary>
	/// <param name="from">The start state</param>
	/// <param name="to">The next state</param>
	/// <param name="t">Weighting of the two states</param>
	/// <returns>A mix of the two frames</returns>
	internal static Disc Lerp(Disc from, Disc to, float t)
	{
		t = Mathf.Clamp01(t);

		if (from == null) return to;
		if (to == null) return from;

		return new Disc()
		{
			position = Vector3.Lerp(from.position.ToVector3(), to.position.ToVector3(), t).ToFloatArray(),
			forward = from.forward == null ? null : Vector3.Lerp(from.forward.ToVector3(), to.forward.ToVector3(), t).ToFloatArray(),
			left = from.forward == null ? null : Vector3.Lerp(from.left.ToVector3(), to.left.ToVector3(), t).ToFloatArray(),
			up = from.forward == null ? null : Vector3.Lerp(from.up.ToVector3(), to.up.ToVector3(), t).ToFloatArray(),
			velocity = Vector3.Lerp(from.velocity.ToVector3(), to.velocity.ToVector3(), t).ToFloatArray(),
			bounce_count = from.bounce_count
		};
	}
}

[Serializable]
public class Team
{
	public Player[] players;
	public string team;
	public bool possession;
	public Stats stats;

	/// <summary>
	/// ↔ Mixes the two states with a linear interpolation based on t
	/// For binary or int values, the "from" state is preferred.
	/// </summary>
	/// <param name="from">The start state</param>
	/// <param name="to">The next state</param>
	/// <param name="t">Weighting of the two states</param>
	/// <returns>A mix of the two frames</returns>
	internal static Team Lerp(Team from, Team to, float t)
	{
		t = Mathf.Clamp01(t);

		Team newTeam = new Team()
		{
			team = from.team,
			possession = from.possession,
			stats = from.stats

		};

		if (from.players == null)
		{
			newTeam.players = null;
		}
		else if (to.players == null)
		{
			newTeam.players = from.players;
		}
		else
		{
			// TODO make sure the players are in the same order. This should only be a problem when players join/leave
			int numPlayers = Math.Max(from.players.Length, to.players.Length);

			newTeam.players = new Player[numPlayers];

			for (int i = 0; i < numPlayers; i++)
			{
				if (from.players.Length <= i &&
					to.players.Length > i)
				{
					newTeam.players[i] = to.players[i];
				}
				else if (to.players.Length <= i &&
				  from.players.Length > i)
				{
					newTeam.players[i] = from.players[i];
				}
				else if (from.players.Length > i &&
				  to.players.Length > i)
				{
					// actually lerp the team
					newTeam.players[i] = Player.Lerp(from.players[i], to.players[i], t);
				}
			}
		}

		return newTeam;
	}

}

[Serializable]
public class Player
{
	public EchoTransform rightHand { get => new EchoTransform(rhand); }
	/// <summary>
	/// Private so that you use the rightHand property instead
	/// </summary>
	public JToken rhand { private get; set; }
	public int playerid;
	public string name;
	public long userid;
	public Stats stats;
	public int number;
	public int level;
	public bool stunned;
	public int ping;
	public bool invulnerable;

	// old api
	public float[] position;
	public float[] left;
	public float[] up;
	public float[] forward;

	public Vector3 Position {
		get {
			if (body != null) return body.Position;
			else return position.ToVector3();
		}
	}
	public Quaternion Rotation {
		get {
			if (body != null) return body.Rotation;
			else return Quaternion.LookRotation(forward.ToVector3(), up.ToVector3());
		}
	}
	public EchoTransform head { private get; set; }
	/// <summary>
	/// In order to support old replay file format
	/// </summary>
	public EchoTransform Head => head ?? new EchoTransform(position, forward, left, up);
	public bool possession;
	public EchoTransform body;
	public EchoTransform leftHand { get => new EchoTransform(lhand); }
	/// <summary>
	/// Private so that you use the leftHand property instead
	/// </summary>
	public JToken lhand { private get; set; }
	public bool blocking;
	public float[] velocity;

	/// <summary>
	/// This is not from the api, but set afterwards in the temporal processing step
	/// </summary>
	public Vector3 playspacePosition = Vector3.zero;
	public float distanceGained = 0;
	public Vector3 virtualPlayspacePosition = Vector3.zero;


	/// <summary>
	/// ↔ Mixes the two states with a linear interpolation based on t
	/// For binary or int values, the "from" state is preferred.
	/// </summary>
	/// <param name="from">The start state</param>
	/// <param name="to">The next state</param>
	/// <param name="t">Weighting of the two states</param>
	/// <returns>A mix of the two frames</returns>
	internal static Player Lerp(Player from, Player to, float t)
	{
		t = Mathf.Clamp01(t);

		var player = new Player()
		{
			rhand = EchoTransform.Lerp(new EchoTransform(from.rhand), new EchoTransform(to.rhand), t).ToJToken(),
			playerid = from.playerid,
			name = from.name,
			userid = from.userid,
			stats = from.stats,
			number = from.number,
			level = from.level,
			stunned = from.stunned,
			ping = from.ping,
			invulnerable = from.invulnerable,

			position = from.position == null ? null : Vector3.Lerp(from.position.ToVector3(), to.position.ToVector3(), t).ToFloatArray(),
			left = from.position == null ? null : Vector3.Lerp(from.left.ToVector3(), to.left.ToVector3(), t).ToFloatArray(),
			up = from.position == null ? null : Vector3.Lerp(from.up.ToVector3(), to.up.ToVector3(), t).ToFloatArray(),
			forward = from.position == null ? null : Vector3.Lerp(from.forward.ToVector3(), to.forward.ToVector3(), t).ToFloatArray(),

			head = EchoTransform.Lerp(from.head, to.head, t),
			possession = from.possession,
			body = EchoTransform.Lerp(from.body, to.body, t),
			lhand = EchoTransform.Lerp(new EchoTransform(from.lhand), new EchoTransform(to.lhand), t).ToJToken(),
			blocking = from.blocking,
			velocity = Vector3.Lerp(from.velocity.ToVector3(), to.velocity.ToVector3(), t).ToFloatArray(),
			playspacePosition = Vector3.Lerp(from.playspacePosition, to.playspacePosition, t)
		};

		return player;
	}
}



[Serializable]
public class Playspace
{

	public float[] vr_left;
	public float[] vr_position;
	public float[] vr_forward;
	public float[] vr_up;

	/// <summary>
	/// ↔ Mixes the two states with a linear interpolation based on t
	/// For binary or int values, the "from" state is preferred.
	/// </summary>
	/// <param name="from">The start state</param>
	/// <param name="to">The next state</param>
	/// <param name="t">Weighting of the two states</param>
	/// <returns>A mix of the two frames</returns>
	internal static Playspace Lerp(Playspace from, Playspace to, float t)
	{
		t = Mathf.Clamp01(t);

		if (from == null || to == null) return null;

		return new Playspace()
		{
			vr_left = Vector3.Lerp(from.vr_left.ToVector3(), to.vr_left.ToVector3(), t).ToFloatArray(),
			vr_position = Vector3.Lerp(from.vr_position.ToVector3(), to.vr_position.ToVector3(), t).ToFloatArray(),
			vr_forward = Vector3.Lerp(from.vr_forward.ToVector3(), to.vr_forward.ToVector3(), t).ToFloatArray(),
			vr_up = Vector3.Lerp(from.vr_up.ToVector3(), to.vr_up.ToVector3(), t).ToFloatArray()
		};
	}
}

/// <summary>
/// Object for position and rotation
/// </summary>
[Serializable]
public class EchoTransform
{
	public EchoTransform() { }
	public EchoTransform(JToken jToken)
	{
		try
		{
			EchoTransform transform = jToken.ToObject<EchoTransform>();

			pos = transform.pos;
			position = transform.position;
			forward = transform.forward;
			left = transform.left;
			up = transform.up;
		}
		catch (JsonSerializationException)
		{
			pos = jToken.ToObject<float[]>();
		}
	}

	public EchoTransform(float[] pos, float[] forward, float[] left, float[] up)
	{
		this.pos = pos;
		position = pos;
		this.forward = forward;
		this.left = left;
		this.up = up;
	}

	[JsonIgnore]
	public Vector3 Position {
		get {
			if (pos != null) return pos.ToVector3();
			else if (position != null) return position.ToVector3();
			else throw new NullReferenceException("Neither pos nor position are set");
		}
	}

	[JsonIgnore]
	public Quaternion Rotation {
		get { return Quaternion.LookRotation(forward.ToVector3(), up.ToVector3()); }
	}
	public float[] pos;
	public float[] position;
	public float[] forward;
	public float[] left;
	public float[] up;

	/// <summary>
	/// ↔ Mixes the two states with a linear interpolation based on t
	/// For binary or int values, the "from" state is preferred.
	/// </summary>
	/// <param name="from">The start state</param>
	/// <param name="to">The next state</param>
	/// <param name="t">Weighting of the two states</param>
	/// <returns>A mix of the two frames</returns>
	internal static EchoTransform Lerp(EchoTransform from, EchoTransform to, float t)
	{
		t = Mathf.Clamp01(t);

		if (from == null || to == null) return null;

		var transform = new EchoTransform()
		{
			pos = from.pos == null ? null : Vector3.Lerp(from.pos.ToVector3(), to.pos.ToVector3(), t).ToFloatArray(),
			position = from.position == null ? null : Vector3.Lerp(from.position.ToVector3(), to.position.ToVector3(), t).ToFloatArray(),
			forward = from.forward == null ? null : Vector3.Lerp(from.forward.ToVector3(), to.forward.ToVector3(), t).ToFloatArray(),
			left = from.left == null ? null : Vector3.Lerp(from.left.ToVector3(), to.left.ToVector3(), t).ToFloatArray(),
			up = from.up == null ? null : Vector3.Lerp(from.up.ToVector3(), to.up.ToVector3(), t).ToFloatArray()
		};

		return transform;
	}

	public JToken ToJToken()
	{
		return JToken.Parse(JsonConvert.SerializeObject(this));
	}
}

static class FloatArrayExtension
{
	public static Vector3 ToVector3(this float[] array)
	{
		return new Vector3(array[2], array[1], array[0]);
	}
	public static Vector3 ToVector3Backwards(this float[] array)
	{
		return new Vector3(array[0], array[1], array[2]);
	}

	public static float[] ToFloatArray(this Vector3 vector3)
	{
		return new float[]
		{
			vector3.z,
			vector3.y,
			vector3.x
		};
	}
}
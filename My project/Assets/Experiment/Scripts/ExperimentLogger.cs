using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Text; using UnityEngine;
public enum ConditionType { Nature, City } public enum ConditionOrder { Random, NatureThenCity, CityThenNature }
[Serializable] public class ExperimentEvent { public string name; public string timestampIso; }
[Serializable] public class SamRatings { public int valence = -1, arousal = -1, dominance = -1; public string timestampIso; public bool IsComplete => valence >= 1 && arousal >= 1 && dominance >= 1; }
[Serializable] public class ExperimentSession {
 public string participantId, sessionId, startedAtIso, finishedAtIso; public bool testMode; public ConditionType firstCondition; public List<ExperimentEvent> events = new List<ExperimentEvent>(); public Dictionary<string,int> ratings = new Dictionary<string,int>();
 public static string NowIso() => DateTime.Now.ToString("o", CultureInfo.InvariantCulture); public static ExperimentSession Create(string id, bool test, ConditionType first) => new ExperimentSession { participantId=id, sessionId=Guid.NewGuid().ToString("N").Substring(0,8), startedAtIso=NowIso(), testMode=test, firstCondition=first };
 public IEnumerable<ConditionType> ConditionSequence() { yield return firstCondition; yield return firstCondition == ConditionType.Nature ? ConditionType.City : ConditionType.Nature; } public void RecordEvent(string name) => events.Add(new ExperimentEvent { name=name, timestampIso=NowIso() }); public void RecordRating(string name,int value) { ratings[name]=value; RecordEvent(name+"_Recorded"); }
}
public static class ExperimentLogger {
#if UNITY_EDITOR
 public static string VerificationDirectory;
#endif
 public static string SaveDirectory {
  get {
#if UNITY_EDITOR
   if (!string.IsNullOrEmpty(VerificationDirectory)) return VerificationDirectory;
#endif
   return Path.Combine(Application.persistentDataPath,"ExperimentData");
  }
 }
 public static string Save(ExperimentSession session) { Directory.CreateDirectory(SaveDirectory); var path=Path.Combine(SaveDirectory,$"participant_{Safe(session.participantId)}_{session.sessionId}.csv"); var csv=new StringBuilder("participant_id,session_id,event_name,event_timestamp,response\n"); foreach(var e in session.events) { var key=e.name.Replace("_Recorded",""); csv.AppendLine(Row(session.participantId,session.sessionId,e.name,e.timestampIso,session.ratings.ContainsKey(key)?session.ratings[key].ToString():"")); } var temporary = path + ".tmp"; File.WriteAllText(temporary,csv.ToString(),Encoding.UTF8); if (File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); Debug.Log("Experiment data saved: "+path); return path; }
 static string Row(params string[] values) => string.Join(",",Array.ConvertAll(values,v=>"\""+(v??"").Replace("\"","\"\"")+"\"")); static string Safe(string v) { if(string.IsNullOrWhiteSpace(v)) return "unknown"; foreach(var c in Path.GetInvalidFileNameChars()) v=v.Replace(c,'_'); return v.Replace(' ','_'); }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using Newtonsoft.Json;

public class SlowMathScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public KMSelectable[] ButtonSels;
    public KMSelectable GoSel;
    public GameObject[] ButtonObj;
    public GameObject GoObj;
    public TextMesh ScreenText;
    public GameObject TimerBarObj;
    public MeshRenderer TimerBarRenderer;
    public Material GoMat;
    public Material ButtonMat;
    public class VoltData { public string voltage { get; set; } }

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly string _possibleLetters = "ABCDEGKNPSTXZ";
    private string[] _triangleTable = new string[169];

    private List<int> _solutionNums;
    private List<string> _chosenLetters;
    private int _stageNum;
    private bool _isActivated;
    private Coroutine _timerCoroutine;
    private const float _maxTime = 60f;
    private const float _addTime = 10f;
    private float _timeRemaining = _maxTime;
    private string _inputString = "";
    private int _stageCount;

    private enum ArrowDirection
    {
        LeftToRight,
        TopRightToBottomLeft,
        TopLeftToBottomRight
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);
        GoSel.OnInteract += GoPress;

        // START RULESEED
        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Slow Math #{0}] Using rule seed {1}.", _moduleId, rnd.Seed);
        var ruleIx = new string[] { "F", "H", "I", "J", "L", "M", "O", "Q", "R", "U", "V", "W", "Y", "#", "#", "#", "#", "#", "#", "#", "#", "#", "#", "#", "#", "#" };
        var remainingRules = new List<string>();
        for (int i = 0; i < 169; i++)
        {
            if (remainingRules.Count == 0)
            {
                remainingRules.AddRange(ruleIx);
                rnd.ShuffleFisherYates(remainingRules);
            }
            var ix = rnd.Next(0, remainingRules.Count);
            var r = rnd.Next(1, 10);
            if (remainingRules[ix] == "#")
                _triangleTable[i] = r.ToString();
            else
                _triangleTable[i] = remainingRules[ix];
            remainingRules.RemoveAt(ix);
        }
        // Debug.Log(_triangleTable.Join(" "));
        // END RULESEED
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null;
        GenerateSolution();
        StartNextStage();
    }

    private KMSelectable.OnInteractHandler ButtonPress(int i)
    {
        return delegate ()
        {
            ButtonSels[i].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ButtonSels[i].transform);
            if (_moduleSolved || !_isActivated)
                return false;
            if (i == 10)
            {
                // this is the submit
                int inputNum;
                if (!int.TryParse(_inputString, out inputNum))
                {
                    Debug.LogFormat("[Slow Math #{0}] Inputted an invalid number. Strike.", _moduleId);
                    Module.HandleStrike();
                    ResetUponStrike();
                }
                else if (inputNum != _solutionNums[_stageNum])
                {
                    Debug.LogFormat("[Slow Math #{0}] Incorrectly inputted {1}. Strike.", _moduleId, inputNum);
                    Module.HandleStrike();
                    ResetUponStrike();
                }
                else
                {
                    Debug.LogFormat("[Slow Math #{0}] Correctly submitted {1}.", _moduleId, inputNum);
                    _stageNum++;
                    if (_stageNum == _stageCount)
                    {
                        _moduleSolved = true;
                        Module.HandlePass();
                        ScreenText.text = "";
                        Audio.PlaySoundAtTransform("disarmed", transform);
                        Debug.LogFormat("[Slow Math #{0}] Module solved.", _moduleId);
                        ResetButtons();
                        TimerBarObj.transform.localScale = new Vector3(1f, 1f, 0f);
                    }
                    else
                    {
                        Audio.PlaySoundAtTransform("passedstage", transform);
                        Debug.LogFormat("[Slow Math #{0}] Moving to Stage {1}.", _moduleId, _stageNum + 1);
                        StartNextStage();
                    }
                }
            }
            else
            {
                _inputString += i.ToString();
                _timeRemaining += _addTime;
                if (_timeRemaining > _maxTime)
                {

                    Debug.LogFormat("[Slow Math #{0}] The time remaining has gone above {1} seconds. Strike.", _moduleId, _maxTime);
                    Module.HandleStrike();
                    ResetUponStrike();
                }
            }
            return false;
        };
    }

    private void ResetUponStrike()
    {
        _isActivated = false;
        _stageNum = 0;
        ResetButtons();
        GenerateSolution();
        StartNextStage();
    }

    private void ResetButtons()
    {
        if (_timerCoroutine != null)
            StopCoroutine(_timerCoroutine);
        GoObj.GetComponent<MeshRenderer>().sharedMaterial = GoMat;
        foreach (var b in ButtonObj)
            b.GetComponent<MeshRenderer>().sharedMaterial = ButtonMat;
    }

    private void StartNextStage()
    {
        ScreenText.text = _chosenLetters[_stageNum];
        _inputString = "";
        TimerBarObj.transform.localScale = new Vector3(1f, 1f, 1f);
        TimerBarRenderer.material.color = Color.green;
        _timeRemaining = _maxTime;
    }

    private bool GoPress()
    {
        GoSel.AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, GoSel.transform);
        if (_moduleSolved || _isActivated)
            return false;
        GoObj.GetComponent<MeshRenderer>().sharedMaterial = ButtonMat;
        foreach (var b in ButtonObj)
            b.GetComponent<MeshRenderer>().sharedMaterial = GoMat;
        _isActivated = true;
        _timerCoroutine = StartCoroutine(DoTimer());
        return false;
    }

    private IEnumerator DoTimer()
    {
        while (_timeRemaining > 0)
        {
            TimerBarRenderer.material.color = Color.Lerp(Color.red, Color.green, _timeRemaining / _maxTime);
            TimerBarObj.transform.localScale = new Vector3(1f, 1f, Mathf.LerpUnclamped(0f, 1f, _timeRemaining / _maxTime));
            yield return null;
            _timeRemaining -= Time.deltaTime;
        }
        Debug.LogFormat("[Slow Math #{0}] Ran out of time. Strike.", _moduleId);
        Module.HandleStrike();
        ResetUponStrike();
    }

    private void GenerateSolution()
    {
        _stageCount = Rnd.Range(3, 6);
        Debug.LogFormat("[Slow Math #{0}] ===============================================", _moduleId);
        Debug.LogFormat("[Slow Math #{0}] There will be {1} stages.", _moduleId, _stageCount);
        tryAgain:
        _solutionNums = new List<int>();
        var solutionNumsPerStage = new List<int>();
        _chosenLetters = new List<string>();
        var intersections = new List<string[]>();
        for (int stage = 0; stage < _stageCount; stage++)
        {
            var letters = new int[3];
            var ixs = new int[3][];
            for (int i = 0; i < letters.Length; i++)
            {
                letters[i] = Rnd.Range(0, 13);
                ixs[i] = GetRowIndices(letters[i], (ArrowDirection)i);
            }
            _chosenLetters.Add(letters.Select(i => _possibleLetters[i]).Join(""));
            var intersectionList = new List<int>();
            for (int i = 0; i < 169; i++)
            {
                int ct = 0;
                for (int j = 0; j < ixs.Length; j++)
                    if (ixs[j].Contains(i))
                        ct++;
                if (ct > 1)
                    intersectionList.Add(i);
            }
            if (intersectionList.Count < 3)
                goto tryAgain;
            intersections.Add(intersectionList.Select(i => _triangleTable[i]).ToArray());

            var solutionStr = "";
            for (int i = 0; i < intersections[stage].Length; i++)
            {
                int num;
                if (int.TryParse(intersections[stage][i], out num))
                {
                    solutionStr += num.ToString();
                    continue;
                }
                switch (intersections[stage][i])
                {
                    case "F":
                        solutionStr += "§ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(BombInfo.GetSerialNumberLetters().First()).ToString();
                        break;
                    case "H":
                        solutionStr += BombInfo.GetIndicators().Where(j => j.Contains('A') || j.Contains('E') || j.Contains('I') || j.Contains('O') || j.Contains('U')).Count().ToString();
                        break;
                    case "I":
                        solutionStr += BombInfo.GetIndicators().Where(j => j.Contains('R')).Count().ToString();
                        break;
                    case "J":
                        var query = BombInfo.QueryWidgets("volt", "");
                        if (query.Count != 0)
                            solutionStr += ((int)float.Parse(JsonConvert.DeserializeObject<VoltData>(query.First()).voltage)).ToString();
                        else
                            solutionStr += (BombInfo.GetModuleNames().Count - BombInfo.GetSolvableModuleNames().Count).ToString();
                        break;
                    case "L":
                        solutionStr += BombInfo.GetSerialNumberNumbers().Sum().ToString();
                        break;
                    case "M":
                        solutionStr += BombInfo.GetSerialNumber()[5].ToString();
                        break;
                    case "O":
                        solutionStr += BombInfo.GetSolvableModuleNames().Count().ToString();
                        break;
                    case "Q":
                        solutionStr += BombInfo.GetOnIndicators().Count().ToString();
                        break;
                    case "R":
                        solutionStr += BombInfo.GetPortPlateCount().ToString();
                        break;
                    case "U":
                        solutionStr += BombInfo.GetIndicators().Count().ToString();
                        break;
                    case "V":
                        solutionStr += BombInfo.GetBatteryCount().ToString();
                        break;
                    case "W":
                        solutionStr += BombInfo.GetPortCount().ToString();
                        break;
                    case "Y":
                        solutionStr += (stage + 1).ToString();
                        break;
                    default:
                        throw new InvalidOperationException("A letter not in the table ended up in the calculation!");
                }
            }
            int sol;
            if (!int.TryParse(solutionStr, out sol))
                goto tryAgain;
            solutionNumsPerStage.Add(sol);
            try
            {
                checked { _solutionNums.Add(stage == 0 ? sol : _solutionNums.Last() + sol); }
            }
            catch (OverflowException)
            {
                goto tryAgain;
            }
        }
        for (int stage = 0; stage < _stageCount; stage++)
        {
            Debug.LogFormat("[Slow Math #{0}] Stage {1}:", _moduleId, stage + 1);
            Debug.LogFormat("[Slow Math #{0}] Chosen letters: {1}.", _moduleId, _chosenLetters[stage].Join(", "));
            Debug.LogFormat("[Slow Math #{0}] Intersections, in reading order: {1}", _moduleId, intersections[stage].Join(", "));
            Debug.LogFormat("[Slow Math #{0}] Solution for this stage: {1}.", _moduleId, solutionNumsPerStage[stage]);
            Debug.LogFormat("[Slow Math #{0}] Calculated solution: {1}.", _moduleId, _solutionNums[stage]);
        }
    }

    private int[] GetRowIndices(int letter, ArrowDirection dir)
    {
        var list = new List<int>();
        if (dir == ArrowDirection.LeftToRight)
        {
            int r = 12 - letter;
            int i = r * r;
            while (i < (r + 1) * (r + 1))
            {
                list.Add(i);
                i++;
            }
        }
        else if (dir == ArrowDirection.TopLeftToBottomRight)
        {
            int r = letter;
            int i = r * r;
            while (i < 169)
            {
                list.Add(i);
                if (list.Count % 2 == 0)
                    i++;
                else
                {
                    i += 2 * r + 2;
                    r++;
                }
            }
        }
        else if (dir == ArrowDirection.TopRightToBottomLeft)
        {
            int r = letter;
            int i = (r + 1) * (r + 1) - 1;
            while (i < 169)
            {
                list.Add(i);
                if (list.Count % 2 == 0)
                    i--;
                else
                {
                    i += 2 * r + 2;
                    r++;
                }
            }
        }
        return list.ToArray();
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} go [Press go] !{0} press 1 2 3 [Press buttons 1, 2, 3] | !{0} submit";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        var m = Regex.Match(command, @"^\s*go\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            GoSel.OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*(press\s+)?submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            ButtonSels[10].OnInteract();
            yield break;
        }
        if (command.StartsWith("press "))
        {
            var cmd = command.Substring(6);
            var list = new List<KMSelectable>();
            var ixs = "0123456789 ";
            for (int i = 0; i < cmd.Length; i++)
            {
                int ix = ixs.IndexOf(cmd[i]);
                if (ix == 10)
                    continue;
                if (ix == -1)
                    yield break;
                list.Add(ButtonSels[ix]);
            }
            yield return null;
            yield return list;
        }
    }

    private void TwitchHandleForcedSolve()
    {
        StartCoroutine(Autosolve());
    }

    private IEnumerator Autosolve()
    {
        if (!_isActivated)
        {
            GoSel.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (!_moduleSolved)
        {
            var str = _solutionNums[_stageNum].ToString();
            if (!_inputString.StartsWith(str))
                _inputString = "";
            while (_inputString.Length < str.Length)
            {
                yield return new WaitUntil(() => _timeRemaining + _addTime <= _maxTime);
                ButtonSels[str[_inputString.Length] - '0'].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            ButtonSels[10].OnInteract();
            if (!_moduleSolved)
                yield return new WaitForSeconds(0.1f);
        }
    }
}

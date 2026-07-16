using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueLine
{
    [Tooltip("이름")]
    public string speakerName;

    [Tooltip("대사")]
    [TextArea(2, 5)]
    public string sentence;

    [Tooltip("비워두면 이전 이미지")]
    public Sprite portrait;
}

[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueSO : ScriptableObject
{
    [Tooltip("정방향")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}
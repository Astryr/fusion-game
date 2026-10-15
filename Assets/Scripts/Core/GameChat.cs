using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public readonly struct ChatEntry
{
    public readonly string Sender;
    public readonly Color Color;
    public readonly string Text;

    public ChatEntry(string sender, Color color, string text)
    {
        Sender = sender;
        Color = color;
        Text = text;
    }
}

public static class GameChat
{
    public const int MaxLength = 100;
    public const float SendCooldown = 0.6f;

    private const int HistoryLimit = 40;
    private const float MinReceiveGap = 0.15f;

    private static readonly List<ChatEntry> History = new List<ChatEntry>();
    private static readonly Dictionary<int, float> LastReceivedAt = new Dictionary<int, float>();
    private static readonly StringBuilder Builder = new StringBuilder(MaxLength);

    public static bool IsTyping { get; set; }
    public static IReadOnlyList<ChatEntry> Entries => History;

    public static event Action<ChatEntry> MessageReceived;

    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        Builder.Clear();
        bool lastWasSpace = true;
        foreach (char c in raw)
        {
            char value = c;
            if (value == '\n' || value == '\r' || value == '\t')
            {
                value = ' ';
            }
            else if (char.IsControl(value) || char.IsSurrogate(value))
            {
                continue;
            }
            else if (value == '<')
            {
                value = '\u2039';
            }
            else if (value == '>')
            {
                value = '\u203A';
            }

            if (value == ' ')
            {
                if (lastWasSpace)
                {
                    continue;
                }

                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            Builder.Append(value);
            if (Builder.Length >= MaxLength)
            {
                break;
            }
        }

        return Builder.ToString().TrimEnd();
    }

    public static void Receive(int senderId, string sender, Color color, string text)
    {
        float now = Time.unscaledTime;
        if (LastReceivedAt.TryGetValue(senderId, out float last) && now - last < MinReceiveGap)
        {
            return;
        }

        LastReceivedAt[senderId] = now;

        var entry = new ChatEntry(Sanitize(sender), color, text);
        History.Add(entry);
        if (History.Count > HistoryLimit)
        {
            History.RemoveAt(0);
        }

        MessageReceived?.Invoke(entry);
    }

    public static void Clear()
    {
        History.Clear();
        LastReceivedAt.Clear();
        IsTyping = false;
    }
}

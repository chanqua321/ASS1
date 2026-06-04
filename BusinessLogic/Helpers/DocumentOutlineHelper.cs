using System.Text;
using System.Text.RegularExpressions;
using BusinessLogic.DTOs;

namespace BusinessLogic.Helpers;

public static class DocumentOutlineHelper
{
    // Heuristic: nhận diện heading theo kiểu tài liệu (VN/EN) + đánh số.
    private static readonly Regex NumberHeading = new(@"^(?<num>\d+(?:\.\d+){0,5})[)\.]?\s+(?<title>\S.+)$",
        RegexOptions.Compiled);

    private static readonly Regex RomanHeading = new(@"^(?<num>[IVXLC]+)[)\.]?\s+(?<title>\S.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ChapterHeading = new(@"^(chương|chapter)\s+\d+(\s*[:\-–]\s*)?(?<title>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<DocumentOutlineNodeDto> BuildOutline(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
            return Array.Empty<DocumentOutlineNodeDto>();

        var lines = extractedText
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var root = new OutlineNode("ROOT", 0);
        var stack = new Stack<OutlineNode>();
        stack.Push(root);

        OutlineNode current = root;
        var buffer = new StringBuilder();

        void FlushBufferToCurrent()
        {
            var t = buffer.ToString().Trim();
            if (t.Length > 0)
                current.Body.Add(t);
            buffer.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (TryParseHeading(line, out var title, out var level))
            {
                FlushBufferToCurrent();

                while (stack.Count > 0 && stack.Peek().Level >= level)
                    stack.Pop();

                var parent = stack.Peek();
                var node = new OutlineNode(title, level);
                parent.Children.Add(node);
                stack.Push(node);
                current = node;
                continue;
            }

            buffer.AppendLine(line);
        }

        FlushBufferToCurrent();

        return root.Children.Select(ToDto).ToList();
    }

    private static bool TryParseHeading(string line, out string title, out int level)
    {
        title = "";
        level = 1;

        // Ignore very long lines as headings
        if (line.Length > 140)
            return false;

        // 1) "Chương 1: ..." / "Chapter 1 - ..."
        var mChap = ChapterHeading.Match(line);
        if (mChap.Success)
        {
            title = line;
            level = 1;
            return true;
        }

        // 2) "1." / "1.1" / "2.3.4"
        var mNum = NumberHeading.Match(line);
        if (mNum.Success)
        {
            var num = mNum.Groups["num"].Value;
            title = $"{num} {mNum.Groups["title"].Value}".Trim();
            level = 1 + num.Count(c => c == '.'); // 1, 1.1, 1.1.1...
            return true;
        }

        // 3) Roman: "I." "II." ...
        var mRoman = RomanHeading.Match(line);
        if (mRoman.Success && mRoman.Groups["num"].Value.Length <= 6)
        {
            title = $"{mRoman.Groups["num"].Value.ToUpperInvariant()} {mRoman.Groups["title"].Value}".Trim();
            level = 1;
            return true;
        }

        // 4) All caps short line → heading (VD: "GIỚI THIỆU", "KẾT LUẬN")
        if (line.Length >= 4 && line.Length <= 60)
        {
            var letters = line.Count(char.IsLetter);
            if (letters >= 4)
            {
                var upperLetters = line.Count(c => char.IsLetter(c) && char.IsUpper(c));
                if (upperLetters / (double)letters >= 0.85)
                {
                    title = line;
                    level = 1;
                    return true;
                }
            }
        }

        return false;
    }

    private static DocumentOutlineNodeDto ToDto(OutlineNode node)
    {
        var preview = string.Join("\n\n", node.Body.Take(2)).Trim();
        if (preview.Length > 900) preview = preview[..900] + "...";

        return new DocumentOutlineNodeDto
        {
            Title = node.Title,
            Level = node.Level,
            ContentPreview = preview,
            Children = node.Children.Select(ToDto).ToList()
        };
    }

    private sealed class OutlineNode
    {
        public OutlineNode(string title, int level)
        {
            Title = title;
            Level = level;
        }

        public string Title { get; }
        public int Level { get; }
        public List<string> Body { get; } = new();
        public List<OutlineNode> Children { get; } = new();
    }
}


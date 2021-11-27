//-----------------------------------------------------------------------
// <copyright file="Selector.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
//     Copyright (C) 2014 The Kubernetes Authors.
// </copyright>
//-----------------------------------------------------------------------

// Direct port of https://github.com/kubernetes/apimachinery/blob/ffb9472ec51a7a702c21d6e30e090b2e466cfe8f/pkg/labels/selector.go

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Akka.Annotations;

namespace Akka.Discovery.KubernetesApi.Internal
{
    internal interface ISelector
    {
        bool Matches(Dictionary<string, string> labels);
        bool Empty();
        ISelector Add(params Selector.Requirement[] r);
        ImmutableList<Selector.Requirement> Requirements { get; }
        (string, bool) RequiresExactMatch(string value);
    }

    internal class RequirementComparer : IComparer<Selector.Requirement>
    {
        public int Compare(Selector.Requirement x, Selector.Requirement y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }
    }

    [InternalApi]
    public enum Token
    {
        Error,
        EndOfString,
        ClosedPar,
        Comma,
        DoesNotExist,
        DoubleEquals,
        Equals,
        GreaterThan,
        Identifier,
        In,
        LessThan,
        NotEquals,
        NotIn,
        OpenPar
    }
    
    internal class Selector : ISelector
    {
        private static readonly RequirementComparer Comparer = new RequirementComparer(); 
        
        public class Requirement : IEquatable<Requirement>
        {
            public readonly string Key;
            public readonly string Operator;
            public readonly string[] Values;

            public Requirement(string key, string op, string[] vals)
            {
                switch (op)
                {
                    case Selection.In:
                    case Selection.NotIn:
                        if(vals.Length == 0)
                            throw new Exception("for 'in' and 'notin' operator, values set can't be empty");
                        break;
                    case Selection.Equals:
                    case Selection.DoubleEquals:
                    case Selection.NotEquals:
                        if (vals.Length != 1)
                            throw new Exception("exact-match compatibility requires one single value");
                        break;
                    case Selection.Exists:
                    case Selection.DoesNotExist:
                        if (vals.Length != 0)
                            throw new Exception("values set must be empty for exists and does not exists");
                        break;
                    case Selection.GreaterThan:
                    case Selection.LessThan:
                        if (vals.Length != 1)
                            throw new Exception("for 'Gt' and 'Lt' operators, exactly one value is required");
                        if (!int.TryParse(vals[0], out _))
                            throw new Exception("for 'Gt' and 'Lt' operators, the value must be an integer");
                        break;
                    case var notSupported:
                        throw new Exception(
                            $"Operation {notSupported} not supported. Valid operators are: {string.Join(", ", ValidRequirementOperators)}");
                }

                Key = key;
                Operator = op;
                Values = vals;
            }

            public bool HasValue(string value)
                => Values.Contains(value);

            public bool Matches(Dictionary<string, string> labels)
            {
                switch (Operator)
                {
                    case Selection.In:
                    case Selection.Equals:
                    case Selection.DoubleEquals:
                    {
                        if (!labels.TryGetValue(Key, out var val))
                            return false;
                        return HasValue(val);
                    }
                    
                    case Selection.NotIn:
                    case Selection.NotEquals:
                    {
                        if (!labels.TryGetValue(Key, out var val))
                            return true;
                        return !HasValue(val);
                    }
                    
                    case Selection.Exists:
                        return labels.Keys.Contains(Key);
                    
                    case Selection.DoesNotExist:
                        return !labels.Keys.Contains(Key);
                    
                    case Selection.GreaterThan:
                    case Selection.LessThan:
                    {
                        if (!labels.TryGetValue(Key, out var val))
                            return false;
                        var lValue = int.Parse(val);
                        var rValue = int.Parse(Values[0]);
                        return (Operator == Selection.GreaterThan && lValue > rValue) ||
                               (Operator == Selection.LessThan && lValue < rValue);
                    }
                    default:
                        return false;
                }
            }

            public bool Equals(Requirement other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                if (Key != other.Key)
                    return false;
                if (Operator != other.Operator)
                    return false;
                return Values.Length == other.Values.Length && Values.All(value => other.Values.Contains(value));
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Requirement req && Equals(req);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Key != null ? Key.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Operator != null ? Operator.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Values != null ? Values.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }        
        
        public sealed class NothingSelector : ISelector
        {
            public static readonly ISelector Instance = new NothingSelector(); 
            private NothingSelector()
            { }
            
            public bool Matches(Dictionary<string, string> labels) => false;
            public bool Empty() => false;
            public ISelector Add(params Requirement[] r) => this;
            public ImmutableList<Requirement> Requirements => null;
            public (string, bool) RequiresExactMatch(string value) => ("", false);
        }

        public static readonly ISelector Everything = new Selector(ImmutableList<Requirement>.Empty);
        
        public static readonly ISelector Nothing = NothingSelector.Instance;
        
        public static readonly ImmutableHashSet<string> UnaryOperators = ImmutableHashSet<string>.Empty
            .Add(Selection.DoesNotExist);

        public static readonly ImmutableHashSet<string> BinaryOperators = ImmutableHashSet<string>.Empty
            .Add(Selection.In).Add(Selection.NotIn)
            .Add(Selection.Equals).Add(Selection.DoubleEquals).Add(Selection.NotEquals)
            .Add(Selection.GreaterThan).Add(Selection.LessThan);

        public static readonly ImmutableHashSet<string> ValidRequirementOperators =
            UnaryOperators.Union(BinaryOperators);
        
        public static class Selection
        {
            public const string DoesNotExist = "!";
            public const string Equals = "=";
            public const string DoubleEquals = "==";
            public const string In = "in";
            public const string NotEquals = "!=";
            public const string NotIn = "notin";
            public const string Exists = "exists";
            public const string GreaterThan = "gt";
            public const string LessThan = "lt";
        }

        
        private readonly ImmutableList<Requirement> _internalSelector;

        private Selector(ImmutableList<Requirement> selector)
        {
            _internalSelector = selector;
        }
        
        public ISelector Add(params Requirement[] r)
        {
            return new Selector(_internalSelector.AddRange(r));
        }

        public bool Matches(Dictionary<string, string> labels)
        {
            return _internalSelector.All(req => req.Matches(labels));
        }

        public bool Empty()
        {
            return _internalSelector == null || _internalSelector.Count == 0;
        }

        public ImmutableList<Requirement> Requirements => _internalSelector;

        public (string, bool) RequiresExactMatch(string label)
        {
            foreach (var req in _internalSelector)
            {
                if (req.Key == label)
                {
                    switch (req.Operator)
                    {
                        case Selection.Equals:
                        case Selection.DoubleEquals:
                        case Selection.In:
                            if (req.Values.Length == 1)
                                return (req.Values[0], true);
                            break;
                    }

                    return ("", false);
                }
            }

            return ("", false);
        }

        public static ISelector Parse(string selector)
        {
            var p = new Parser(selector);
            var items = p.Parse();
            items.Sort(Comparer);
            return new Selector(items.ToImmutableList());
        }
        
        public class Parser
        {
            public struct ScannedItem
            {
                public Token Token;
                public string Literal;

                public ScannedItem(Token token, string literal)
                {
                    Token = token;
                    Literal = literal;
                }

                public void Deconstruct(out Token token, out string literal)
                {
                    token = Token;
                    literal = Literal;
                }
            }
            
            public class Lexer
            {
                private string _s;
                private int _pos;

                public Lexer(string s)
                {
                    _s = s;
                }

                public char Read()
                {
                    if (_pos >= _s.Length)
                        return (char)0;
                    
                    var b = _s[_pos];
                    _pos++;
                    return b;
                }

                public void Unread()
                {
                    _pos--;
                }

                public (Token, string) ScanIdOrKeyword()
                {
                    var buffer = new StringBuilder();
                    var loop = true;
                    do
                    {
                        switch (Read())
                        {
                            case (char) 0:
                                loop = false;
                                break;
                            case var ch when char.IsWhiteSpace(ch) || IsSpecialSymbol(ch):
                                Unread();
                                loop = false;
                                break;
                            case var ch:
                                buffer.Append(ch);
                                break;
                        }
                    } while (loop);

                    var str = buffer.ToString();
                    return StringToToken.TryGetValue(str, out var token) ? (token, str) : (Token.Identifier, str);
                }

                public (Token, string) ScanSpecialSymbol()
                {
                    var lastToken = Token.Error;
                    var lastLiteral = "";
                    var buffer = string.Empty;
                    var loop = true;
                    do
                    {
                        switch (Read())
                        {
                            case (char) 0:
                                loop = false;
                                break;
                            case var ch when IsSpecialSymbol(ch):
                                buffer += ch;
                                if (StringToToken.TryGetValue(buffer, out var token))
                                {
                                    lastToken = token;
                                    lastLiteral = buffer;
                                    continue;
                                }
                                
                                if(lastToken != Token.Error)
                                {
                                    Unread();
                                    loop = false;
                                }
                                break;
                            default:
                                Unread();
                                loop = false;
                                break;
                        }
                    } while (loop);

                    if (lastToken == Token.Error)
                        return (Token.Error, $"error expected: keyword found {buffer}");

                    return (lastToken, lastLiteral);
                }

                public char SkipWhiteSpaces(char ch)
                {
                    while (true)
                    {
                        if (!char.IsWhiteSpace(ch))
                            return ch;
                        ch = Read();
                        if (ch == (char) 0)
                            return ch;
                    }
                }

                public (Token, string) Lex()
                {
                    var ch = SkipWhiteSpaces(Read());
                    if (ch == (char) 0)
                        return (Token.EndOfString, "");
                    if (IsSpecialSymbol(ch))
                    {
                        Unread();
                        return ScanSpecialSymbol();
                    }
                    Unread();
                    return ScanIdOrKeyword();
                }
            }
            
            public enum ParserContext
            {
                KeyAndOperator,
                Values
            }
        
            public static readonly Dictionary<string, Token> StringToToken = new Dictionary<string, Token>
            {
                [")"] = Token.ClosedPar,
                [","] = Token.Comma,
                ["!"] = Token.DoesNotExist,
                ["=="] = Token.DoubleEquals,
                ["="] = Token.Equals,
                [">"] = Token.GreaterThan,
                ["in"] = Token.In,
                ["<"] = Token.LessThan,
                ["!="] = Token.NotEquals,
                ["notin"] = Token.NotIn,
                ["("] = Token.OpenPar
            };

            public static readonly char[] Symbols = "=!(),><".ToCharArray();
            public static bool IsSpecialSymbol(char c)
                => Symbols.Contains(c);

            
            private readonly Lexer _lexer;
            private readonly List<ScannedItem> _scannedItems;
            private int _position;
            private string _path;

            public Parser(string s)
            {
                _lexer = new Lexer(s);
                _scannedItems = new List<ScannedItem>();
                _position = 0;
            }

            public (Token, string) LookAhead(ParserContext context)
            {
                var (tok, lit) = _scannedItems[_position];
                if (context == ParserContext.Values && (tok == Token.In || tok == Token.NotIn))
                    tok = Token.Identifier;
                return (tok, lit);
            }

            public (Token, string) Consume(ParserContext context)
            {
                var (tok, lit) = _scannedItems[_position];
                _position++;
                if (context == ParserContext.Values && (tok == Token.In || tok == Token.NotIn))
                    tok = Token.Identifier;
                return (tok, lit);
            }

            // scan runs through the input string and stores the ScannedItem in an array
            // Parser can now lookahead and consume the tokens
            public void Scan()
            {
                while (true)
                {
                    var (token, literal) = _lexer.Lex();
                    _scannedItems.Add(new ScannedItem(token, literal));
                    if (token == Token.EndOfString)
                        break;
                }
            }
            
            // parse runs the left recursive descending algorithm
            // on input string. It returns a list of Requirement objects.
            public List<Requirement> Parse()
            {
                Scan();
                var requirements = new List<Requirement>();
                while (true)
                {
                    var (tok, lit) = LookAhead(ParserContext.Values);
                    switch (tok)
                    {
                        case Token.Identifier:
                        case Token.DoesNotExist:
                            try
                            {
                                requirements.Add(ParseRequirement());
                            }
                            catch (Exception e)
                            {
                                throw new Exception($"unable to parse requirement: {e.Message}", e);
                            }
                            var (t, l) = Consume(ParserContext.Values);
                            switch (t)
                            {
                                case Token.EndOfString:
                                    return requirements;
                                case Token.Comma:
                                    var (t2, l2) = LookAhead(ParserContext.Values);
                                    if (t2 != Token.Identifier && t2 != Token.DoesNotExist)
                                        throw new Exception($"found {l2}, expected: identifier after ','");
                                    continue;
                                default:
                                    throw new Exception($"found {l}, expected ',' or 'end of string'");
                            }

                        case Token.EndOfString:
                            return requirements;
                        default:
                            throw new Exception($"found {lit}, expected !, identifier, or 'end of string'");
                    }
                }
            }

            public Requirement ParseRequirement()
            {
                var (key, oper) = ParseKeyAndInferOperator();
                if (oper == Selection.Exists || oper == Selection.DoesNotExist)
                {
                    return new Requirement(key, oper, new string[0]);
                }

                oper = ParseOperator(); 

                HashSet<string> values = null;
                switch (oper)
                {
                    case Selection.In:
                    case Selection.NotIn:
                        values = ParseValues();
                        break;
                    case Selection.Equals:
                    case Selection.DoubleEquals:
                    case Selection.NotEquals:
                    case Selection.GreaterThan:
                    case Selection.LessThan:
                        values = ParseExactValue();
                        break;
                    default:
                        throw new Exception($"invalid operation {oper}");
                }
                return new Requirement(key, oper, values.ToArray());
            }

            public (string, string) ParseKeyAndInferOperator()
            {
                var oper = string.Empty;
                var (tok, literal) = Consume(ParserContext.Values);
                
                if (tok == Token.DoesNotExist)
                {
                    oper = Selection.DoesNotExist;
                    (tok, literal) = Consume(ParserContext.Values);
                }

                if (tok != Token.Identifier)
                    throw new Exception($"found {literal}, expected: identifier");

                var (t, _) = LookAhead(ParserContext.Values);
                if ((t == Token.EndOfString || t == Token.Comma) && oper != Selection.DoesNotExist)
                    oper = Selection.Exists;
                return (literal, oper);
            }

            public string ParseOperator()
            {
                var (tok, lit) = Consume(ParserContext.KeyAndOperator);
                var op = tok switch
                {
                    Token.In => Selection.In,
                    Token.Equals => Selection.Equals,
                    Token.DoubleEquals => Selection.DoubleEquals,
                    Token.GreaterThan => Selection.GreaterThan,
                    Token.LessThan => Selection.LessThan,
                    Token.NotIn => Selection.NotIn,
                    Token.NotEquals => Selection.NotEquals,
                    _ => null
                };
                return op ?? throw new Exception($"found '{lit}', expected {string.Join(", ", BinaryOperators)}");
            }

            public HashSet<string> ParseValues()
            {
                var (tok, lit) = Consume(ParserContext.Values);
                if (tok != Token.OpenPar)
                    throw new Exception($"found '{lit}', expected '('");
                
                (tok, lit) = LookAhead(ParserContext.Values);
                switch (tok)
                {
                    case Token.Identifier:
                    case Token.Comma:
                        var s = ParseIdentifiersList();
                        (tok, _) = Consume(ParserContext.Values);
                        if (tok != Token.ClosedPar)
                            throw new Exception($"found '{lit}', expected ')'");
                        return s;
                    case Token.ClosedPar:
                        Consume(ParserContext.Values);
                        return new HashSet<string>();
                    default:
                        throw new Exception($"found '{lit}'m expected ',', ')', or identifier");
                }
            }

            public HashSet<string> ParseIdentifiersList()
            {
                var s = new HashSet<string>();
                while (true)
                {
                    var (tok, lit) = Consume(ParserContext.Values);
                    switch (tok)
                    {
                        case Token.Identifier:
                            s.Add(lit);
                            var (tok2, lit2) = LookAhead(ParserContext.Values);
                            switch (tok2)
                            {
                                case Token.Comma:
                                    continue;
                                case Token.ClosedPar:
                                    return s;
                                default:
                                    throw new Exception($"found '{lit2}', expected ',' or ')'");
                            }
                        case Token.Comma:
                            if(s.Count == 0)
                                s.Add("");
                            (tok2, _) = LookAhead(ParserContext.Values);
                            switch (tok2)
                            {
                                case Token.ClosedPar:
                                    s.Add("");
                                    return s;
                                case Token.Comma:
                                    s.Add("");
                                    Consume(ParserContext.Values);
                                    break;
                            }
                            break;
                        default:
                            throw new Exception($"found '{lit}', expected ',', or identifier");
                    }
                }
            }

            public HashSet<string> ParseExactValue()
            {
                var s = new HashSet<string>();
                var (tok, lit) = LookAhead(ParserContext.Values);
                if (tok == Token.EndOfString || tok == Token.Comma)
                {
                    s.Add("");
                    return s;
                }

                (tok, lit) = Consume(ParserContext.Values);
                if (tok == Token.Identifier)
                {
                    s.Add(lit);
                    return s;
                }

                throw new Exception($"found '{lit}', expected: identifier");
            }
            
        }        
    }
}
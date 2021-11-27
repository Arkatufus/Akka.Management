using System;
using System.Collections.Generic;
using Akka.Discovery.KubernetesApi.Internal;
using FluentAssertions;
using Xunit;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class SelectorSpec
    {
        [Theory(DisplayName = "Good selectors should pass")]
        [InlineData("x=a,y=b,z=c")]
        [InlineData("")]
        [InlineData("x!=a,y=b")]
        [InlineData("x=")]
        [InlineData("x= ")]
        [InlineData("x=,z= ")]
        [InlineData("x= ,z= ")]
        [InlineData("!x")]
        [InlineData("x>1")]
        [InlineData("x>1,z<5")]
        public void GoodSelectorShouldPass(string s)
        {
            Selector.Parse(s);
        }

        [Theory(DisplayName = "Bad selectors should throw")]
        [InlineData("x=a||y=b")]
        [InlineData("x==a==b")]
        [InlineData("!x=a")]
        [InlineData("x<a")]
        public void BadSelectorsShouldThrow(string s)
        {
            Assert.Throws<Exception>(() => Selector.Parse(s));
        }

        [Fact(DisplayName = "Selector should be deterministic")]
        public void ShouldBeDeterministic()
        {
            var s1 = Selector.Parse("x=a,a=x");
            var s2 = Selector.Parse("a=x,x=a");
            s1.Requirements[0].Should().Be(s2.Requirements[0]);
            s1.Requirements[1].Should().Be(s2.Requirements[1]);
        }

        [Fact(DisplayName = "Everything should match everything")]
        public void EverythingShouldMatchEverything()
        {
            Selector.Everything.Matches(Set("x", "y")).Should().BeTrue();
            Selector.Everything.Matches(Set()).Should().BeTrue();
            Selector.Everything.Matches(Set("x", "y", "z", "w")).Should().BeTrue();
        }

        private static Dictionary<string, string> Set(params string[] args)
        {
            var dict = new Dictionary<string, string>();
            for (var i = 0; i < args.Length; i += 2)
            {
                dict[args[i]] = args[i + 1];
            }
            return dict;
        }

        [Theory(DisplayName = "Selector should match correct labels")]
        [InlineData("", "x", "y")]
        [InlineData("x=y", "x", "y")]
        [InlineData("x=y,z=w", "x", "y", "z", "w")]
        [InlineData("x!=y,z!=w", "x", "z", "z", "a")]
        [InlineData("notin=in", "notin", "in")]
        [InlineData("x", "x", "z")]
        [InlineData("!x", "y", "z")]
        [InlineData("x>1", "x", "2")]
        [InlineData("x<1", "x", "0")]
        [InlineData("foo=bar", "foo", "bar", "baz", "blah")]
        [InlineData("baz=blah", "foo", "bar", "baz", "blah")]
        [InlineData("foo=bar,baz=blah", "foo", "bar", "baz", "blah")]
        [InlineData("a in ( b )", "a", "a", "a", "b")]
        [InlineData("a in ( b, c )", "a", "a", "a", "b")]
        [InlineData("a notin ( b )", "a", "a", "a", "d")]
        [InlineData("a notin ( b, c )", "a", "a", "a", "d")]
        [InlineData("a=b, b in ( c )", "a", "b", "b", "c")]
        public void ExpectMatch(string selector, params string[] args)
        {
            var lq = Selector.Parse(selector);
            lq.Matches(Set(args)).Should().BeTrue();
        }

        [Theory(DisplayName = "Selector should not match incorrect labels")]
        [InlineData("x=z")]
        [InlineData("x=y", "x", "z")]
        [InlineData("x=y,z=w", "x", "w", "z", "w")]
        [InlineData("x!=y,z!=w", "x", "z", "z", "w")]
        [InlineData("x", "y", "z")]
        [InlineData("!x", "x", "z")]
        [InlineData("x>1", "x", "0")]
        [InlineData("x<1", "x", "2")]
        [InlineData("foo=blah", "foo", "bar", "baz", "blah")]
        [InlineData("baz=bar", "foo", "bar", "baz", "blah")]
        [InlineData("foo=bar,foobar=bar,baz=blah", "foo", "bar", "baz", "blah")]
        [InlineData("a in ( b )", "a", "a", "a", "c")]
        [InlineData("a in ( b, c )", "a", "a", "a", "d")]
        [InlineData("a notin ( b )", "a", "a", "a", "b")]
        [InlineData("a notin ( b, c )", "a", "a", "a", "b")]
        [InlineData("a=b, b in ( c )", "a", "c", "b", "c")]
        [InlineData("a=b, b in ( c )", "a", "b", "b", "d")]
        public void ExpectNoMatch(string selector, params string[] args)
        {
            var lq = Selector.Parse(selector);
            lq.Matches(Set(args)).Should().BeFalse();
        }

        [Theory(DisplayName = "Lexer should lex proper tokens")]
        [InlineData("", Token.EndOfString)]
        [InlineData(",", Token.Comma)]
        [InlineData("notin", Token.NotIn)]
        [InlineData("in", Token.In)]
        [InlineData("=", Token.Equals)]
        [InlineData("==", Token.DoubleEquals)]
        [InlineData(">", Token.GreaterThan)]
        [InlineData("<", Token.LessThan)]
        [InlineData("!", Token.DoesNotExist)]
        [InlineData("!=", Token.NotEquals)]
        [InlineData("(", Token.OpenPar)]
        [InlineData(")", Token.ClosedPar)]
        [InlineData("~", Token.Identifier)]
        [InlineData("||", Token.Identifier)]
        public void LexerTest(string s, Token t)
        {
            var l = new Selector.Parser.Lexer(s);
            var (token, lit) = l.Lex();
            token.Should().Be(t);
            lit.Should().Be(s);
        }

        [Theory(DisplayName = "Lexer should lex proper sequence")]
        [InlineData("key in ( value )", Token.Identifier, Token.In, Token.OpenPar, Token.Identifier, Token.ClosedPar)]
        [InlineData("key notin ( value )", Token.Identifier, Token.NotIn, Token.OpenPar, Token.Identifier, Token.ClosedPar)]
        [InlineData("key in ( value1, value2 )", Token.Identifier, Token.In, Token.OpenPar, Token.Identifier, Token.Comma, Token.Identifier, Token.ClosedPar)]
        [InlineData("key", Token.Identifier)]
        [InlineData("!key", Token.DoesNotExist, Token.Identifier)]
        [InlineData("()", Token.OpenPar, Token.ClosedPar)]
        [InlineData("x in (),y", Token.Identifier, Token.In, Token.OpenPar, Token.ClosedPar, Token.Comma, Token.Identifier)]
        [InlineData("== != (), = notin", Token.DoubleEquals, Token.NotEquals, Token.OpenPar, Token.ClosedPar, Token.Comma, Token.Equals, Token.NotIn)]
        [InlineData("key>2", Token.Identifier, Token.GreaterThan, Token.Identifier)]
        [InlineData("key<1", Token.Identifier, Token.LessThan, Token.Identifier)]
        public void LexerSequenceTest(string s, params Token[] ts)
        {
            var l = new Selector.Parser.Lexer(s);
            var tokens = new List<Token>();
            while (true)
            {
                var (token, _) = l.Lex();
                if (token == Token.EndOfString)
                    break;
                tokens.Add(token);
            }

            tokens.Count.Should().Be(ts.Length);
            for (int i = 0; i < ts.Length; ++i)
            {
                tokens[i].Should().Be(ts[i]);
            }
        }
    }
}
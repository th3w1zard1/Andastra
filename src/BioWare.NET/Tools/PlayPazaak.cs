using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py
    // Original: Pazaak card game implementation and rules
    public static class CardType
    {
        public const string Positive = "+";
        public const string Negative = "-";
        public const string PosOrNeg = "+/-";
        public const string YellowSpecial = "Yellow";
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:31-55
    // Original: @dataclass class PazaakSideCard
    public class PazaakSideCard
    {
        public object Value { get; set; } // int or List<int>
        public string CardType { get; set; }

        public PazaakSideCard(object value, string cardType)
        {
            Value = value;
            CardType = cardType;
        }

        public override string ToString()
        {
            if (CardType == BioWare.NET.Tools.CardType.YellowSpecial)
            {
                return $"Yellow {Value}";
            }
            string valueStr = Value.ToString();
            if (CardType == BioWare.NET.Tools.CardType.Positive)
            {
                return $"+{valueStr}";
            }
            if (CardType == BioWare.NET.Tools.CardType.Negative)
            {
                return $"-{valueStr}";
            }
            if (CardType == BioWare.NET.Tools.CardType.PosOrNeg)
            {
                return $"+/-{valueStr}";
            }
            return valueStr;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:41-55
        // Original: def get_value(self, choice: str | None = None) -> int:
        public int GetValue(string choice = null)
        {
            if (CardType == BioWare.NET.Tools.CardType.Positive)
            {
                return (int)Value;
            }
            if (CardType == BioWare.NET.Tools.CardType.Negative)
            {
                return -(int)Value;
            }
            if (CardType == BioWare.NET.Tools.CardType.PosOrNeg)
            {
                return choice == "+" ? (int)Value : -(int)Value;
            }
            if (CardType == BioWare.NET.Tools.CardType.YellowSpecial)
            {
                if (Value is List<int> list && list.Count > 0)
                {
                    return list[0];
                }
            }
            throw new ArgumentException($"Unknown card_type {CardType}");
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:58-75
    // Original: @dataclass class Player
    public class Player
    {
        public string Name { get; set; }
        public List<object> Hand { get; set; } // List<int | PazaakSideCard>
        public List<PazaakSideCard> SideDeck { get; set; }
        public List<PazaakSideCard> ActiveSideHand { get; set; }
        public int Score { get; set; }
        public bool Stands { get; set; }

        public Player(string name)
        {
            Name = name;
            Hand = new List<object>();
            SideDeck = new List<PazaakSideCard>();
            ActiveSideHand = new List<PazaakSideCard>();
            Score = 0;
            Stands = false;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:67-68
        // Original: def calculate_hand_value(self) -> int:
        public int CalculateHandValue()
        {
            int sum = 0;
            foreach (object card in Hand)
            {
                if (card is PazaakSideCard sideCard)
                {
                    sum += sideCard.GetValue();
                }
                else if (card is int intCard)
                {
                    sum += intCard;
                }
            }
            return sum;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:70-71
        // Original: def is_bust(self) -> bool:
        public bool IsBust()
        {
            return CalculateHandValue() > 20;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:73-75
        // Original: def reset_hand(self):
        public void ResetHand()
        {
            Hand.Clear();
            Stands = false;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:78-212
    // Original: class PazaakGame
    public class PazaakGame
    {
        public static readonly List<int> MainDeckValues = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        public const int MaxHandValue = 20;
        public const int SetsToWin = 3;

        public List<int> Deck { get; set; }
        public Player Player { get; set; }
        public Player Ai { get; set; }
        public Player CurrentPlayer { get; set; }
        public Player Winner { get; set; }

        public PazaakGame()
        {
            Deck = CreateDeck();
            Player = new Player("Player");
            Ai = new Player("AI");
            CurrentPlayer = Player;
            Winner = null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:90-95
        // Original: def create_deck(self) -> list[int]:
        public List<int> CreateDeck()
        {
            List<int> deck = new List<int>();
            foreach (int card in MainDeckValues)
            {
                for (int i = 0; i < 4; i++)
                {
                    deck.Add(card);
                }
            }
            // Shuffle
            Random rng = new Random();
            deck = deck.OrderBy(x => rng.Next()).ToList();
            return deck;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:97-100
        // Original: def setup_game(self):
        public void SetupGame()
        {
            Player.SideDeck = ChooseSideDeck();
            Ai.SideDeck = AutoChooseSideDeck();
            ResetRound();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:102-109
        // Original: def reset_round(self):
        public void ResetRound()
        {
            Deck = CreateDeck();
            Player.ResetHand();
            Ai.ResetHand();
            Random rng = new Random();
            Player.ActiveSideHand = ChooseSideDeck().OrderBy(x => rng.Next()).Take(4).ToList();
            Ai.ActiveSideHand = ChooseSideDeck().OrderBy(x => rng.Next()).Take(4).ToList();
            CurrentPlayer = Player;
            Winner = null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:111-133
        // Original: def choose_side_deck(self) -> list[PazaakSideCard]:
        public List<PazaakSideCard> ChooseSideDeck()
        {
            List<PazaakSideCard> sideDeck = new List<PazaakSideCard>
            {
                new PazaakSideCard(1, CardType.PosOrNeg),
                new PazaakSideCard(2, CardType.PosOrNeg),
                new PazaakSideCard(3, CardType.PosOrNeg),
                new PazaakSideCard(4, CardType.PosOrNeg),
                new PazaakSideCard(5, CardType.PosOrNeg),
                new PazaakSideCard(6, CardType.PosOrNeg),
                new PazaakSideCard(1, CardType.Positive),
                new PazaakSideCard(2, CardType.Positive),
                new PazaakSideCard(3, CardType.Positive),
                new PazaakSideCard(4, CardType.Positive),
                new PazaakSideCard(5, CardType.Positive),
                new PazaakSideCard(6, CardType.Positive),
                new PazaakSideCard(1, CardType.Negative),
                new PazaakSideCard(2, CardType.Negative),
                new PazaakSideCard(3, CardType.Negative),
                new PazaakSideCard(4, CardType.Negative),
                new PazaakSideCard(5, CardType.Negative),
                new PazaakSideCard(6, CardType.Negative),
                new PazaakSideCard(new List<int> { 3, 6 }, CardType.YellowSpecial),
            };
            Random rng = new Random();
            return sideDeck.OrderBy(x => rng.Next()).Take(10).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:135-136
        // Original: def auto_choose_side_deck(self) -> list[PazaakSideCard]:
        public List<PazaakSideCard> AutoChooseSideDeck()
        {
            return ChooseSideDeck();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:138-139
        // Original: def draw_card(self) -> int:
        public int DrawCard()
        {
            int card = Deck[Deck.Count - 1];
            Deck.RemoveAt(Deck.Count - 1);
            return card;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:141-146
        // Original: def play_card(self, player: Player, card: int | PazaakSideCard) -> None:
        public void PlayCard(Player player, object card)
        {
            player.Hand.Add(card);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:148-157
        // Original: def apply_yellow_card_effect(self, player: Player, yellow_card: PazaakSideCard) -> None:
        public void ApplyYellowCardEffect(Player player, PazaakSideCard yellowCard)
        {
            List<int> yellowValues = yellowCard.Value as List<int>;
            if (yellowValues == null) return;

            for (int i = 0; i < player.Hand.Count; i++)
            {
                object card = player.Hand[i];
                if (card is PazaakSideCard sideCard && sideCard.CardType == CardType.Positive && yellowValues.Contains((int)sideCard.Value))
                {
                    player.Hand[i] = new PazaakSideCard(sideCard.Value, CardType.Negative);
                }
                else if (card is int intCard && yellowValues.Contains(intCard))
                {
                    player.Hand[i] = new PazaakSideCard(intCard, CardType.Negative);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:159-160
        // Original: def switch_player(self):
        public void SwitchPlayer()
        {
            CurrentPlayer = CurrentPlayer == Player ? Ai : Player;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:162-175
        // Original: def check_winner(self) -> Player | None:
        public Player CheckWinner()
        {
            if (Player.IsBust())
            {
                return Ai;
            }
            if (Ai.IsBust())
            {
                return Player;
            }
            if (Player.Stands && Ai.Stands)
            {
                int playerValue = Player.CalculateHandValue();
                int aiValue = Ai.CalculateHandValue();
                if (playerValue > aiValue)
                {
                    return Player;
                }
                if (aiValue > playerValue)
                {
                    return Ai;
                }
                return null; // Tie
            }
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:177-184
        // Original: def update_score(self, winner: Player | None) -> None:
        public void UpdateScore(Player winner)
        {
            if (winner != null)
            {
                winner.Score++;
            }
            if (winner != null && winner.Score >= SetsToWin)
            {
                Winner = winner;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:186-212
        // Original: def ai_strategy(self) -> tuple[str, PazaakSideCard | None]:
        public Tuple<string, PazaakSideCard> AiStrategy()
        {
            int aiValue = Ai.CalculateHandValue();
            int playerValue = Player.CalculateHandValue();
            Tuple<PazaakSideCard, int> bestChoice = null;
            float minValueDiff = float.MaxValue;

            foreach (PazaakSideCard sideCard in Ai.ActiveSideHand)
            {
                int simulatedValue;
                if (sideCard.CardType == CardType.YellowSpecial)
                {
                    List<object> simulatedHand = new List<object>(Ai.Hand);
                    Player simulatedPlayer = new Player("Simulated") { Hand = simulatedHand };
                    ApplyYellowCardEffect(simulatedPlayer, sideCard);
                    simulatedValue = simulatedPlayer.CalculateHandValue();
                }
                else
                {
                    simulatedValue = aiValue + sideCard.GetValue();
                }

                int valueDiff = MaxHandValue - simulatedValue;
                if (valueDiff >= 0 && valueDiff < minValueDiff)
                {
                    bestChoice = new Tuple<PazaakSideCard, int>(sideCard, simulatedValue);
                    minValueDiff = valueDiff;
                }
            }

            if (bestChoice != null)
            {
                PazaakSideCard sideCard = bestChoice.Item1;
                int newValue = bestChoice.Item2;
                if (newValue == MaxHandValue || (aiValue < playerValue && newValue > aiValue))
                {
                    return new Tuple<string, PazaakSideCard>("use_side_card", sideCard);
                }
            }

            if (aiValue >= 17 && aiValue >= playerValue)
            {
                return new Tuple<string, PazaakSideCard>("stand", null);
            }
            return new Tuple<string, PazaakSideCard>("hit", null);
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:215-239
    // Original: class PazaakInterface(ABC)
    public interface IPazaakInterface
    {
        void SetupGame();
        void PlayTurn(Player player);
        void EndRound(Player winner);
        void EndGame(Player winner);
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/playpazaak.py:397-413
    // Original: def print_game_rules():
    public static class PazaakRules
    {
        public static void PrintGameRules()
        {
            Console.WriteLine(@"
Pazaak Game Rules:
1. The goal is to reach exactly 20 points or get as close as possible without going over.
2. Players take turns drawing cards from the main deck and optionally playing cards from their side deck.
3. Main deck cards are always positive values from 1 to 10.
4. Side deck cards can be positive, negative, or special cards.
5. Players can choose to ""stand"" (keep their current score) or continue playing.
6. If a player's score goes over 20, they ""bust"" and lose the round.
7. The first player to win 3 rounds wins the game.

Special Cards:
- +/- cards: Can be played as either positive or negative.
- Yellow cards: Can flip the sign of a card already on the table.

Good luck and have fun!
");
        }
    }
}

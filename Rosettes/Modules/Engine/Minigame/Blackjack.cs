namespace Rosettes.Modules.Engine.Minigame
{
    public class Blackjack
    {
        private List<Card> _cards = [];
        private readonly int decks;
        private readonly Random _rand = new();

        public Blackjack(int decks)
        {
            this.decks = decks;
            PopulateCards();
            ShuffleCards();
        }

        private void PopulateCards()
        {
            for (int j = 0; j < 4; j++) // per each type of card
            {
                for (int k = 0; k < 12; k++) // per each number and type
                {
                    _cards.Add(new Card(j, k)); // add a card
                }
            }
            // if more than 1 deck, repeat them
            if (decks > 1)
            {
                _cards = (from e in Enumerable.Range(0, decks)
                         from x in _cards
                         select x)
                         .ToList();
            }
        }

        private void ShuffleCards()
        {
            int n = _cards.Count;

            while (n > 1)
            {
                int k = _rand.Next(n);
                n--;
                (_cards[n], _cards[k]) = (_cards[k], _cards[n]);
            }
        }
    }

    public static class CardPropieties
    {
        public static Dictionary<int, (char symbol, string name)> types = new()
        {
            { 0, ('♣', "club") },
            { 1, ('♦', "diamond") },
            { 2, ('♥', "heart") },
            { 3, ('♠', "spade") }
        };
    }

    public class Card(int type, int number)
    {
        public string GetName()
        {
            return $"[{CardPropieties.types[type].symbol}] {number} of {CardPropieties.types[type].name}";
        }

        public int[] GetValue()
        {
            // TODO:
            return new int[] { 0 };
        }
    }
}

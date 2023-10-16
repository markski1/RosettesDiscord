namespace Rosettes.Modules.Engine.Minigame
{
    public class Blackjack
    {
        private List<Card> cards = new();
        private int decks;
        private readonly Random rand = new();

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
                    cards.Add(new Card(j, k)); // add a card
                }
            }
            // if more than 1 deck, repeat them
            if (decks > 1)
            {
                cards = (from e in Enumerable.Range(0, decks)
                         from x in cards
                         select x)
                         .ToList();
            }
        }

        private void ShuffleCards()
        {
            int n = cards.Count;

            while (n > 1)
            {
                int k = rand.Next(n);
                n--;
                (cards[n], cards[k]) = (cards[k], cards[n]);
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

    public class Card
    {
        private readonly int type;
        private readonly int number;

        public Card(int type, int number)
        {
            this.type = type;
            this.number = number;
        }

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

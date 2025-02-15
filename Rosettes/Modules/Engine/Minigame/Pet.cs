using Rosettes.Core;

namespace Rosettes.Modules.Engine.Minigame;

public class Pet
{
    public int Id;
    public int Index;
    private int _timesPet;
    private int _exp;
    private readonly int _foundDate;
    private int _happiness;
    public ulong OwnerId;
    private string _name;

    public int LastInteracted = 0;
    public int LastPet = 0;
    public int LastFed = 0;
    public bool SyncUpToDate = false;

    public Pet(int index, ulong owner_id, string name)
    {
        Index = index;
        OwnerId = owner_id;
        _name = name;
        _foundDate = Global.CurrentUnix();
        _timesPet = 0;
        _exp = 0;
        _happiness = 100;
    }

    public Pet(int pet_id, int pet_index, ulong owner_id, string pet_name, int exp, int times_pet, int found_date, int happiness)
    {
        Id = pet_id;
        Index = pet_index;
        OwnerId = owner_id;
        _name = pet_name;
        _timesPet = times_pet;
        _exp = exp;
        _foundDate = found_date;
        _happiness = happiness;
        // if pet was never initialized, last interaction will be set as now.
        if (_foundDate == 0)
        {
            _foundDate = Global.CurrentUnix();
        }
    }

    public string GetEmoji()
    {
        return PetEngine.PetEmojis(Index);
    }

    // If the animal can be pet, apply the appropiate effects and return the gained happiness.
    // Otherwise return a negative.
    public int DoPet()
    {
        if (Global.CurrentUnix() <= LastPet) return -1;
            
        LastPet = Global.CurrentUnix() + 30;
        int happiness = Global.Randomize(10) + 5;
        ModifyHappiness(+happiness); // add anywhere from 5 to 14% happiness
        AddExp(1);
        _timesPet++;
        SyncUpToDate = false;
        return happiness;
    }

    public int DoFeed(string foodItem)
    {
        if (!PetEngine.AcceptablePetMeal(foodItem))
        {
            return -1; // Error: Pets may only be fed fish of any type, shrimps or carrots
        }

        if (Global.CurrentUnix() <= LastFed) {
            return -2; // Error: Pets may only be fed once in a 5 minute window.
        }
        
        LastFed = Global.CurrentUnix() + 300;
        int happinessMod = Global.Randomize(10) + 5;
        ModifyHappiness(+happinessMod); // add anywhere from 5 to 14% happiness
        AddExp(1);
        _timesPet++;
        SyncUpToDate = false;
        return happinessMod;
    }

    public void ModifyHappiness(int modify)
    {
        _happiness += modify;
        if (_happiness > 100) _happiness = 100;
        else if (_happiness < 0) _happiness = 0;
    }

    // Returns the pet's custom name, or the generic animal name if the pet has not been named.
    public string GetName()
    {
        if (_name != "[not named]")
        {
            return $"{PetEngine.PetEmojis(Index)} {_name}";
        }

        return PetEngine.PetNames(Index);
    }

    public void AddExp(int exp)
    {
        _exp += exp;
    }

    public int GetExp()
    {
        return _exp;
    }

    public int GetTimesPet()
    {
        return _timesPet;
    }

    public string GetBareName()
    {
        return _name;
    }

    public void SetName(string newName)
    {
        _name = newName;
        SyncUpToDate = false;
    }

    public int GetFoundDate()
    {
        return _foundDate;
    }

    internal int GetHappiness()
    {
        return _happiness;
    }
}
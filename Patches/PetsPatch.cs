﻿namespace TOHE;

public static class PetsPatch
{
    public static void RpcRemovePet(this PlayerControl pc)
    {
        if (pc == null || !pc.Data.IsDead) return;
        if (!GameStates.IsInGame) return;
        if (!Options.RemovePetsAtDeadPlayers.GetBool()) return;
        if (!Main.UseVersionProtocol.Value) return;
        if (pc.CurrentOutfit.PetId == "") return;

        pc.RpcSetPet("");
    }
}

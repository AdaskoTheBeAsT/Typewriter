namespace App;

public record DerivedRecordVm(string Id, string Label, int Extra) : BaseRecordVm(Id, Label);

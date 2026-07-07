// typewriter-template: v1
${
    Template(Settings settings)
    {
        settings.IncludeProject("Contracts");
    }
}
$Classes(*Dto)[
export class $Name {$Properties[
    public $name: $Type;]
}
]

// typewriter-template: v1
${
    string RecordExtends(Record record)
    {
        if (record.BaseRecord != null)
        {
            return " extends " + record.BaseRecord.Name;
        }

        return "";
    }
}
$Records(*DerivedRecordVm)[
export class $Name$RecordExtends { }
]

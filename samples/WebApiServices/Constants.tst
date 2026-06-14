// typewriter-template: v1
// output: generated/constants.ts
${
    string ConstantValue(Constant constant)
    {
        return constant.Type == "string"
            ? $"'{constant.Value}'"
            : constant.Value;
    }
}
$Classes(Static)[
export const $Name = {$Constants[
  $name: $ConstantValue,]
} as const;
]

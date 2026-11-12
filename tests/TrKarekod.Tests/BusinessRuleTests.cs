using TrKarekod.Tlv;

namespace TrKarekod.Tests;

public sealed class BusinessRuleTests
{
    private const string ValidIban = "TR330006100519786457841326";

    [Fact]
    public void The_baseline_fixture_is_valid()
    {
        var result = Karekod.Parse(Fast().Build());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Diagnostics));
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Dynamic_code_needs_an_amount()
    {
        Codes(Fast().Without("54")).Should().Contain(DiagnosticCodes.DynamicWithoutAmount);
        Codes(Fast().Set("54", "000000000000")).Should().Contain(DiagnosticCodes.DynamicWithoutAmount);
    }

    [Fact]
    public void Static_code_may_leave_the_amount_to_the_payer()
    {
        var result = Karekod.Parse(Fast().Set("01", "11").Without("54").Build());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Diagnostics));
        result.Merchant!.Amount.Should().BeNull();
    }

    [Fact]
    public void Dynamic_code_needs_an_expiry()
    {
        Codes(Fast().Identity(i => i.Remove("07"))).Should().Contain(DiagnosticCodes.DynamicWithoutExpiry);
    }

    [Theory]
    [InlineData("261231235959", "261231235959")]
    [InlineData("261231235959", "261231000000")]
    public void Expiry_must_follow_creation(string created, string expires)
    {
        Codes(Fast().Identity(i => { i["06"] = created; i["07"] = expires; })).Should().Contain(DiagnosticCodes.ExpiryBeforeCreation);
    }

    [Theory]
    [InlineData("261332000000")]
    [InlineData("260230120000")]
    [InlineData("261231250000")]
    public void Timestamps_must_be_real(string created)
    {
        Codes(Fast().Identity(i => i["06"] = created)).Should().Contain(DiagnosticCodes.InvalidTimestamp);
    }

    [Theory]
    [InlineData("TR33000610051978645784132X", DiagnosticCodes.IbanFormat)]
    [InlineData("DE89370400440532013000ABCD", DiagnosticCodes.IbanFormat)]
    [InlineData("TR3300061005197864578413", DiagnosticCodes.LengthOutOfRange)]
    public void Malformed_iban_is_an_error(string iban, string code)
    {
        Codes(Fast().Account(a => a["01"] = iban)).Should().Contain(code);
    }

    [Fact]
    public void Iban_with_a_bad_check_digit_is_a_warning()
    {
        var result = Karekod.Parse(Fast().Account(a => a["01"] = "TR340006100519786457841326").Build());

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == DiagnosticCodes.IbanChecksum);
        result.Merchant.Should().NotBeNull();
    }

    [Fact]
    public void Fast_is_always_in_lira()
    {
        Codes(Fast().Set("53", "840")).Should().Contain(DiagnosticCodes.InvalidValue);
    }

    [Fact]
    public void Fast_needs_identity_and_payment_purpose()
    {
        Codes(Fast().Set("01", "11").Without("51")).Should().Contain(DiagnosticCodes.MissingField);
        Codes(Fast().Without("62")).Should().Contain(DiagnosticCodes.MissingField);
    }

    [Fact]
    public void Refund_needs_the_original_reference_and_purpose_00()
    {
        var refund = Fast().Account(a => a["02"] = "04");

        Codes(refund).Should().Contain(DiagnosticCodes.RefundRules);
        Codes(refund.Set("31", "01282012180960000000000000123456").Additional(a => a["08"] = "00")).Should().BeEmpty();
        Codes(refund.Set("31", "01282012180960000000000000123456")).Should().Contain(DiagnosticCodes.RefundRules);
    }

    [Fact]
    public void Refund_reference_must_be_well_formed()
    {
        Codes(Fast().Set("31", "0128201318096000000000000012345X")).Should().Contain(DiagnosticCodes.InvalidRefundReference);
    }

    [Theory]
    [InlineData("399394233285179")]
    [InlineData("3993942332851791X")]
    [InlineData("9993942332851791")]
    public void Location_must_be_well_formed(string location)
    {
        var codes = Codes(Fast().Set("50", location));
        codes.Should().Contain(c => c == DiagnosticCodes.InvalidLocation || c == DiagnosticCodes.LengthOutOfRange || c == DiagnosticCodes.InvalidCharacters);
    }

    [Fact]
    public void A_merchant_account_template_is_required()
    {
        Codes(Fast().Without("30")).Should().Contain(DiagnosticCodes.NoMerchantAccount);
    }

    [Fact]
    public void Invalid_codes_have_no_typed_projection()
    {
        Karekod.Parse(Fast().Without("54").Build()).Merchant.Should().BeNull();
    }

    private static List<string> Codes(FastFixture fixture) =>
        Karekod.Parse(fixture.Build()).Errors.Select(e => e.Code).ToList();

    private static FastFixture Fast() => new();

    /// <summary>A valid dynamic FAST code as editable field maps, so each test changes exactly one thing.</summary>
    private sealed class FastFixture
    {
        private readonly SortedDictionary<string, string> _root = new(StringComparer.Ordinal)
        {
            ["00"] = "01",
            ["01"] = "12",
            ["52"] = "5499",
            ["53"] = "949",
            ["54"] = "000000015050",
            ["58"] = "TR",
            ["59"] = "ABC GIDA",
            ["60"] = "ANKARA",
        };

        private readonly SortedDictionary<string, string> _account = new(StringComparer.Ordinal)
        {
            ["00"] = "TR.GOV.TCMB.FAST",
            ["01"] = ValidIban,
            ["02"] = "01",
            ["20"] = "E200C014A30EFCDC7E9F379CE0766A68",
        };

        private readonly SortedDictionary<string, string> _identity = new(StringComparer.Ordinal)
        {
            ["00"] = "10",
            ["02"] = "0010",
            ["03"] = "23451017",
            ["06"] = "261112103000",
            ["07"] = "261112104000",
        };

        private readonly SortedDictionary<string, string> _additional = new(StringComparer.Ordinal) { ["08"] = "09" };
        private static readonly string[] Templates = ["30", "51", "62"];
        private readonly HashSet<string> _removed = [];

        public FastFixture Set(string id, string value)
        {
            var copy = Clone();
            copy._root[id] = value;
            return copy;
        }

        public FastFixture Without(string id)
        {
            var copy = Clone();
            copy._root.Remove(id);
            copy._removed.Add(id);
            return copy;
        }

        public FastFixture Account(Action<IDictionary<string, string>> change) => Edit(c => change(c._account));

        public FastFixture Identity(Action<IDictionary<string, string>> change) => Edit(c => change(c._identity));

        public FastFixture Additional(Action<IDictionary<string, string>> change) => Edit(c => change(c._additional));

        public string Build()
        {
            var writer = new TlvWriter();
            var ids = _root.Keys.Concat(Templates.Where(t => !_removed.Contains(t))).Order(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                switch (id)
                {
                    case "30": writer.WriteTemplate(id, t => WriteAll(t, _account)); break;
                    case "51": writer.WriteTemplate(id, t => WriteAll(t, _identity)); break;
                    case "62": writer.WriteTemplate(id, t => WriteAll(t, _additional)); break;
                    default: writer.Write(id, _root[id]); break;
                }
            }

            return writer.WriteCrcAndFinish();
        }

        private static void WriteAll(TlvWriter writer, IDictionary<string, string> fields)
        {
            foreach (var (id, value) in fields)
            {
                writer.Write(id, value);
            }
        }

        private FastFixture Edit(Action<FastFixture> change)
        {
            var copy = Clone();
            change(copy);
            return copy;
        }

        private FastFixture Clone()
        {
            var copy = new FastFixture();
            Copy(_root, copy._root);
            Copy(_account, copy._account);
            Copy(_identity, copy._identity);
            Copy(_additional, copy._additional);
            copy._removed.UnionWith(_removed);
            return copy;
        }

        private static void Copy(SortedDictionary<string, string> from, SortedDictionary<string, string> to)
        {
            to.Clear();
            foreach (var (k, v) in from)
            {
                to[k] = v;
            }
        }
    }
}

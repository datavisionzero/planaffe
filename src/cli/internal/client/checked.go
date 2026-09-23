package client

import (
	"fmt"
	"net/http"
	"reflect"

	"github.com/datavisionzero/planaffe/src/cli/internal/exit"
)

// Checked is the call and the check that follow each other at nearly every
// request pa makes: an error from the HTTP client is Transport's, and an
// answer is Check's. It takes the call's two results whole, so a verb reads
//
//	resp, err := client.Checked(c.ReadIssueWithResponse(ctx, key))
//
// and a nil error means resp is the success the contract promises.
//
// The generated responses share no interface — each is its own struct with
// the same two fields, `HTTPResponse` and `Body` — so those two are read by
// name. A type without them is a generator that changed its mind, and it is
// answered as the bug in pa it would be rather than as a panic.
func Checked[R any](resp *R, err error) (*R, error) {
	if err != nil {
		return nil, Transport(err)
	}

	answer, body, ok := parts(resp)
	if !ok {
		return nil, &Failure{Code: exit.Unexpected, Message: fmt.Sprintf("pa cannot read a %T; this is a bug in pa", resp)}
	}
	if err := Check(answer, body); err != nil {
		return nil, err
	}
	return resp, nil
}

func parts(resp any) (*http.Response, []byte, bool) {
	value := reflect.ValueOf(resp)
	if value.Kind() != reflect.Pointer || value.IsNil() || value.Elem().Kind() != reflect.Struct {
		return nil, nil, false
	}

	answerField, bodyField := value.Elem().FieldByName("HTTPResponse"), value.Elem().FieldByName("Body")
	if !answerField.IsValid() || !bodyField.IsValid() || !answerField.CanInterface() || !bodyField.CanInterface() {
		return nil, nil, false
	}
	answer, ok := answerField.Interface().(*http.Response)
	if !ok {
		return nil, nil, false
	}
	body, ok := bodyField.Interface().([]byte)
	return answer, body, ok
}
